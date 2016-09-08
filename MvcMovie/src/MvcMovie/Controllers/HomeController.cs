using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MvcMovie.Data;
using MvcMovie.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace MvcMovie.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;

        }

        //GET: Home
        [Authorize]
        public async Task<IActionResult> Index(int? transType, string searchString, decimal startingBalance = 0M)
        {
            //get user id on home aciton hit
            var userId = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            //if the starting balance in the box is not empty on index load
            if(startingBalance >= 0)
            {
                //try to update useraccount record
                try
                {
                    //check for existing useraccount record
                    var userAccount = await _context.tUserAccount.SingleOrDefaultAsync(u => u.UserID == userId);

                    if(userAccount == null)
                    {
                        //no record yet, create one
                        Account userAccountRecord = new Account { UserID = userId, StartingBalance = (decimal)startingBalance };
                        _context.Add(userAccountRecord);
                    }
                    else
                    {
                        //record found, update w/ balance, only if there is a value other than 0 provided
                        if(startingBalance != 0M)
                        {
                            //existing user account, update
                            userAccount.StartingBalance = (decimal)startingBalance;
                            _context.Update(userAccount);
                        }
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    //carry through the rest of the index method
                }
                catch(Exception ex)
                {
                    throw;
                }
            }

            var trans = from t in _context.Trans
                        where t.userID == userId
                        select t;

            if (!string.IsNullOrEmpty(searchString))
            {
                trans = trans.Where(transaction => transaction.description.Contains(searchString));
            }

            if (transType != null && transType >= 0)
            {
                trans = trans.Where(transaciton => transaciton.transType == (enumTransType)transType);
            }

            var accountVM = new AccountViewModel();
            accountVM.userAccount = await _context.tUserAccount.SingleOrDefaultAsync(u => u.UserID == userId);
            accountVM.transTypes = new List<enumTransType> { enumTransType.Expense, enumTransType.Income};
            accountVM.lstTransactions = await trans.ToListAsync();

            accountVM.lstTransactions.OrderBy(t => t.transDate).ThenBy(t => t.transType);

            accountVM.lstTransactions.Sort((x, y) => DateTime.Compare(x.transDate, y.transDate));

            //processing for projected balance
            //we need to get starting balance from user
            //this will come in the form of a static 'bank balance' field entered in the index page

            //get all incomes that's coming in ref to 'today'
            //cannot be one time incomes, as those will be calculated as 'money i got / am getting before my next pay'
            //if it's money they 'got' in the past, then they should have 'gotten it' and updated their startbal accordingly
            //so no need to get any possible entries in the past
            //users will delete the old entries, or we will clean them, asking user if they received/paid bill before deleting
            List<Trans> income = accountVM.lstTransactions.Where(x => x.transType == enumTransType.Income
                                                                && x.transFrequency != enumTransFrequency.OneTime
                                                                && x.transDate >= DateTime.Today).ToList();

            //get the 1st one, as you are trying to find how much $ you will have between now and next pay
            Trans nextIncome = income.FirstOrDefault();

            //get any 'one time incomes' between now and PAYDAY
            List<Trans> oneTimeIncome = accountVM.lstTransactions.Where(x => x.transType == enumTransType.Income
                                                                        && x.transFrequency == enumTransFrequency.OneTime
                                                                        && x.transDate < nextIncome.transDate
                                                                        && x.transDate >= DateTime.Today).ToList();

            var moneyIn = 0M;

            //get all one time incomes between now and PAYDAY
            //this is where we will pull our 'current bank balance from'
            //user will have to enter valid bank balance if/when they want accurate readings
            foreach (var i in oneTimeIncome)
            {
                moneyIn += i.value;
            }

            //now we have a balance of how much money we *should* have come PAYDAY, 
            //let's see if we will meet all our bills before next PAYDAY,
            //and how much money we will have to play with

            //get a list of upcoming expenses between now and payday
            List<Trans> expenses = accountVM.lstTransactions.Where(x => x.transType == enumTransType.Expense
                                                                    && x.transDate < nextIncome.transDate).ToList();

            var moneyOut = 0M;

            foreach (var e in expenses)
            {
                moneyOut += e.value;
            }

            accountVM.userAccount.ProjectedBalance = accountVM.userAccount.StartingBalance + moneyIn - moneyOut;

            return View(accountVM);
        }

        // GET: Home/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Home/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create([Bind("ID,description,value,transDate,transType,transFrequency")] Trans transaction)
        {
            if (ModelState.IsValid)
            {
                #region Switch On TransFrequency OneTime vs. ... Scheduled
                switch(transaction.transFrequency)
                {
                    case (enumTransFrequency.OneTime):
                        //do one time
                        transaction.userID = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        _context.Add(transaction);
                        await _context.SaveChangesAsync();
                        return RedirectToAction("Index");
                    case (enumTransFrequency.Daily):
                    case (enumTransFrequency.Weekly):
                    case (enumTransFrequency.BiWeekly):
                    case (enumTransFrequency.Monthly):
                    case (enumTransFrequency.Yearly):
                        #region Switch On TransType Income vs. Expense
                        switch (transaction.transType)
                        {
                            case (enumTransType.Expense):
                            case (enumTransType.Income):
                                {
                                    //do recurring expense
                                    //for the next 6 months, take transaction amount, and transaction frequency
                                    //fill db with calendar entries of transaction
                                    switch (transaction.transFrequency)
                                    {
                                        #region Daily
                                        case (enumTransFrequency.Daily):
                                            {
                                                //do daily
                                                List<Trans> transactionsToAdd = new List<Trans>();

                                                //get date of trans as per user entry
                                                var date = transaction.transDate.Date;

                                                //configurable time span, make user option to refresh and do another 6m
                                                var monthsAhead = 6;
                                                var frequencyInDays = 1;

                                                //get time in the future to stop logging the trans so we dont infinite log
                                                var monthsFromTransactionDate = transaction.transDate.AddMonths(monthsAhead);

                                                //from now, as long as we are less than the time above, increasing <DAILY>, add a transaction
                                                for (DateTime i = date; i < monthsFromTransactionDate; i = i.AddDays(frequencyInDays))
                                                {
                                                    transactionsToAdd.Add(
                                                        new Trans
                                                        {
                                                            description = transaction.description,//get dscr from user
                                                    transDate = i,//set date to be either (a) what user entereed AND THEN incrementing auto until 6m from now
                                                    transFrequency = transaction.transFrequency,//log what the frequency is for (to display + calc on refresh)
                                                    transType = transaction.transType,//expense? used for styling
                                                    value = transaction.value,//value of i/o
                                                    userID = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                                        }
                                                    );
                                                }

                                                //add the trans to the db
                                                _context.Trans.AddRange(transactionsToAdd);
                                                await _context.SaveChangesAsync();
                                                return RedirectToAction("Index");
                                            }
                                        #endregion
                                        #region Weekly
                                        case (enumTransFrequency.Weekly):
                                            {
                                                //do weekly
                                                List<Trans> transactionsToAdd = new List<Trans>();

                                                //get date of trans as per user entry
                                                var date = transaction.transDate.Date;

                                                //configurable time span, make user option to refresh and do another 6m
                                                var monthsAhead = 6;
                                                var frequencyInDays = 7;//weekly would be once every 7 days

                                                //get time in the future to stop logging the trans so we dont infinite log
                                                var monthsFromTransactionDate = transaction.transDate.AddMonths(monthsAhead);

                                                //from now, as long as we are less than the time above, increasing <Weekly>, add a transaction
                                                for (DateTime i = date; i < monthsFromTransactionDate; i = i.AddDays(frequencyInDays))
                                                {
                                                    transactionsToAdd.Add(
                                                        new Trans
                                                        {
                                                            description = transaction.description,//get dscr from user
                                                    transDate = i,//set date to be either (a) what user entereed AND THEN incrementing auto until 6m from now
                                                    transFrequency = transaction.transFrequency,//log what the frequency is for (to display + calc on refresh)
                                                    transType = transaction.transType,//expense? used for styling
                                                    value = transaction.value,//value of i/o
                                                    userID = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                                        }
                                                    );
                                                }

                                                //add the trans to the db
                                                _context.Trans.AddRange(transactionsToAdd);
                                                await _context.SaveChangesAsync();
                                                return RedirectToAction("Index");
                                            }
                                        #endregion
                                        #region BiWeekly
                                        case (enumTransFrequency.BiWeekly):
                                            {
                                                //do biweekly
                                                List<Trans> transactionsToAdd = new List<Trans>();

                                                //get date of trans as per user entry
                                                var date = transaction.transDate.Date;

                                                //configurable time span, make user option to refresh and do another 6m
                                                var monthsAhead = 6;
                                                var frequencyInDays = 14;//14 days would be biweekly (every two weeks (7days))

                                                //get time in the future to stop logging the trans so we dont infinite log
                                                var monthsFromTransactionDate = transaction.transDate.AddMonths(monthsAhead);

                                                //from now, as long as we are less than the time above, increasing <BiWeekly>, add a transaction
                                                for (DateTime i = date; i < monthsFromTransactionDate; i = i.AddDays(frequencyInDays))
                                                {
                                                    transactionsToAdd.Add(
                                                        new Trans
                                                        {
                                                            description = transaction.description,//get dscr from user
                                                    transDate = i,//set date to be either (a) what user entereed AND THEN incrementing auto until 6m from now
                                                    transFrequency = transaction.transFrequency,//log what the frequency is for (to display + calc on refresh)
                                                    transType = transaction.transType,//expense? used for styling
                                                    value = transaction.value,//value of i/o
                                                    userID = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                                        }
                                                    );
                                                }

                                                //add the trans to the db
                                                _context.Trans.AddRange(transactionsToAdd);
                                                await _context.SaveChangesAsync();
                                                return RedirectToAction("Index");
                                            }
                                        #endregion
                                        case (enumTransFrequency.Monthly):
                                            {
                                                //do monthly
                                                List<Trans> transactionsToAdd = new List<Trans>();

                                                //get date of trans as per user entry
                                                var date = transaction.transDate.Date;

                                                //configurable time span, make user option to refresh and do another 6m
                                                var monthsAhead = 6;
                                                var frequencyInMonths = 1;//1 month ahead

                                                //get time in the future to stop logging the trans so we dont infinite log
                                                var monthsFromTransactionDate = transaction.transDate.AddMonths(monthsAhead);

                                                //from now, as long as we are less than the time above, increasing <Monthly>, add a transaction
                                                for (DateTime i = date; i < monthsFromTransactionDate; i = i.AddMonths(frequencyInMonths))
                                                {
                                                    transactionsToAdd.Add(
                                                        new Trans
                                                        {
                                                            description = transaction.description,//get dscr from user
                                                    transDate = i,//set date to be either (a) what user entereed AND THEN incrementing auto until 6m from now
                                                    transFrequency = transaction.transFrequency,//log what the frequency is for (to display + calc on refresh)
                                                    transType = transaction.transType,//expense? used for styling
                                                    value = transaction.value,//value of i/o
                                                    userID = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                                        }
                                                    );
                                                }

                                                //add the trans to the db
                                                _context.Trans.AddRange(transactionsToAdd);
                                                await _context.SaveChangesAsync();
                                                return RedirectToAction("Index");
                                            }
                                        case (enumTransFrequency.Yearly):
                                            {
                                                //do yearly
                                                List<Trans> transactionsToAdd = new List<Trans>();

                                                //get date of trans as per user entry
                                                var date = transaction.transDate.Date;

                                                //configurable time span, make user option to refresh and do another 6m
                                                var yearsAhead = 2;//give them two years so at least one gets logged
                                                var frequencyInYears = 1;//1 month ahead

                                                //get time in the future to stop logging the trans so we dont infinite log
                                                var monthsFromTransactionDate = transaction.transDate.AddYears(yearsAhead);

                                                //from now, as long as we are less than the time above, increasing <Monthly>, add a transaction
                                                for (DateTime i = date; i < monthsFromTransactionDate; i = i.AddYears(frequencyInYears))
                                                {
                                                    transactionsToAdd.Add(
                                                        new Trans
                                                        {
                                                            description = transaction.description,//get dscr from user
                                                    transDate = i,//set date to be either (a) what user entereed AND THEN incrementing auto until 6m from now
                                                    transFrequency = transaction.transFrequency,//log what the frequency is for (to display + calc on refresh)
                                                    transType = transaction.transType,//expense? used for styling
                                                    value = transaction.value,//value of i/o
                                                    userID = this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                                        }
                                                    );
                                                }

                                                //add the trans to the db
                                                _context.Trans.AddRange(transactionsToAdd);
                                                await _context.SaveChangesAsync();
                                                return RedirectToAction("Index");
                                            }
                                        default:
                                            break;
                                    }
                                    break;
                                }
                            default:
                                break;
                        }
                        #endregion
                        break;
                }
                #endregion

            }
            return View(transaction);
        }

        // GET: Home/Delete/5
        [Authorize]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var trans = await _context.Trans.SingleOrDefaultAsync(t => t.ID == id);
            if (trans == null)
            {
                return NotFound();
            }

            return View(trans);
        }

        // POST: Home/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var trans = await _context.Trans.SingleOrDefaultAsync(m => m.ID == id);
            _context.Trans.Remove(trans);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View();
        }
    }
}
