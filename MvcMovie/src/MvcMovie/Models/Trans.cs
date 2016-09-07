using MvcMovie.Data;
using System;
using System.ComponentModel.DataAnnotations;

namespace MvcMovie.Models
{
    public class Trans
    {
        public int ID { get; set; }

        [Display(Name ="Description")]
        [StringLength(60, MinimumLength=3)]
        public string description { get; set; }

        [Display(Name ="Value")]
        [Range(1, 100000)]
        [DataType(DataType.Currency)]
        public decimal value { get; set; }

        [Display(Name ="Transaction Date")]
        [DataType(DataType.Date)]
        public DateTime transDate { get; set; }

        [Display(Name ="Type")]
        public enumTransType transType { get; set; }

        [Display(Name ="Frequency")]
        public enumTransFrequency transFrequency {get;set;}

        public string userID { get; set; }

    }
}
