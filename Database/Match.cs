using System;
using System.ComponentModel.DataAnnotations;

namespace Database
{
    public class Match
    {
        [Key]
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public string HomeTeamName  { get; set; }
        public string AwayTeamName { get; set; }
        public int HomeTeamGoals { get; set; }
        public int AwayTeamGoals { get; set; }
    }
}
