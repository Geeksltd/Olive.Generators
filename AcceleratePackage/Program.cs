using System;

namespace OliveGenerator
{
    class Program
    {
        static int Main(string[] args)
        {
            Log.Initialize(args);
            Timings.Initialize(args);

            Log.Run("Accelerate Package Restore",
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3), args);

            // The help screen exists but was never reached: a bad command line just exited.
            if (!ParametersParser.Start(args))
            {
                var usage = Helper.ShowHelp();
                Log.Shutdown(usage);
                return usage;
            }

            // Kept, so that the run's closing record can name the code the process exits with.
            var exitCode = -1;

            try
            {
                ParametersParser.LoadParameters();

                Log.Field("command", Context.Command);
                Log.Field("file", Context.FileName);
                Log.Field("base path", Context.BasePath);

                using (Log.Step(Context.Command + " packages"))
                {
                    if (Context.Command == Command.Extract) Extract.Start();
                    else Restore.Start();
                }

                exitCode = 0;
            }
            catch (Exception ex)
            {
                Log.Error(ex);
                exitCode = -1;
            }
            finally
            {
                Timings.Report();
                Log.Shutdown(exitCode);
            }

            return exitCode;
        }
    }
}