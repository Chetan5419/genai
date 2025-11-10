using System.Collections.Generic;
using System.Collections;

namespace jpmc_genai
{
    public class LoginCreate
    {
        public string username { get; set; }
        public string password { get; set; }
    }

    public class LoginResponse
    {
        public string userid { get; set; }
        public string role { get; set; }
        public string token { get; set; }
        public List<Project> projects { get; set; }
    }

    public class Project
    {
        public string projectid { get; set; }
        public string title { get; set; }
        public string startdate { get; set; }
        public string projecttype { get; set; }
        public string description { get; set; }
    }

    public class RegisterCreate
    {
        public string name { get; set; }
        public string mail { get; set; }
        public string password { get; set; }
        public string role { get; set; }
    }

    public class TestCase
    {
        public string testcaseid { get; set; }
        public string testdesc { get; set; }
        public string pretestid { get; set; }
        public string prereq { get; set; }
        public List<string> tag { get; set; }
        public List<string> projectid { get; set; }
    }

    public class TestCaseSteps
    {
        public string testcaseid { get; set; }
        public List<string> steps { get; set; }
        public List<string> args { get; set; }
        public int stepnum { get; set; }
    }

    public class TestPlan
    {
        public string current_testid { get; set; }
        public Dictionary<string, Dictionary<string, string>> pretestid_steps { get; set; }
        public Dictionary<string, string> current_bdd_steps { get; set; }
        public Dictionary<string, string> pretestid_scripts { get; set; }
    }

    public class ExecutionLog
    {
        public string exeid { get; set; }
        public string testcaseid { get; set; }
        public string scripttype { get; set; }
        public string datestamp { get; set; }
        public string exetime { get; set; }
        public string message { get; set; }
        public string output { get; set; }
        public string status { get; set; }
    }
}
