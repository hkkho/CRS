using System;

namespace ReactorSim.BrowserHost
{
    internal static class Program
    {
        public static void Main()
        {
            // The browser entry point is main.mjs. The empty managed Main is
            // retained so the generated browser runtime has a normal startup
            // target while all public calls stay behind JSExport methods.
        }
    }
}
