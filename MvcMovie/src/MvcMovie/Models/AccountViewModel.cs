using Microsoft.AspNetCore.Mvc.Rendering;
using MvcMovie.Data;
using MvcMovie.Models;
using System.Collections.Generic;

namespace MvcMovie.Models
{
    public class AccountViewModel
    {
        public List<Trans> lstTransactions;
        public List<enumTransType> transTypes;
        public string transType { get; set; }
        public Account userAccount;
    }
}
