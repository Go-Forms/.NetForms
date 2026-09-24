namespace Sklad
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "film")
            {
                Film.Shoot(args[1]);
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
