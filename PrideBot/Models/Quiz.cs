using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrideBot.Models
{
    public class Quiz
    {
        [PrimaryKey]
        public int QuizId { get; set; }
        public int Day { get; set; }
        public string Submitter { get; set; }
        public string Category { get; set; }
        public string Question { get; set; }
        public string Incorrect { get; set; }
        public string Correct { get; set; }
    }
}
