using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrideBot.Models
{
    public class QuizLog
    {
        [PrimaryKey]
        public string UserId { get; set; }
        [PrimaryKey]
        public int Day { get; set; }
        public int QuizId{ get; set; }
        public bool Attempted { get; set; }
        public bool Correct { get; set; }
        public int Guesses { get; set; }
        public string Guess1 { get; set; }
        public string Guess2 { get; set; }
        public string Guess3 { get; set; }
    }
}
