namespace jpmc_genai
{
    public static class Session
    {
        public static LoginResponse CurrentUser { get; set; }
        public static string Token { get; set; }
        public static Project CurrentProject { get; set; }
    }
}
