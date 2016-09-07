/*
 * Account.cs
 * 
 * This class will hold model properties of the account that will be displayed on the main controller / index action
 * It will hold a list of expenses, a list of incomes, and a diff, for a particular 'pay period'
 * 
 * Version 1.0
 * 
 * $History
 * 
 * 06-09-2016
 * Version 1.0
 * Skylar Barth wsbarth92@gmail.com
 * Initial
 * 
 * */


namespace MvcMovie.Models
{
    public class Account
    {
        public int ID { get; set; }
        public string UserID { get; set; }
        public decimal StartingBalance { get; set; }
        public decimal ProjectedBalance { get; set; }
        
    }
}
