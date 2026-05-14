#if DEBUG
using System;
using System.Threading.Tasks;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.DebugTools.Simulations
{
    public class ExceptionSimulationService
    {
        public void SimulateUIException()
        {
            throw new Exception("DEBUG: Simulated UI Thread Exception");
        }

        public void SimulateBackgroundException()
        {
            Task.Run(() => 
            {
                throw new Exception("DEBUG: Simulated Background Task Exception");
            });
        }

        public async Task SimulateAsyncException()
        {
            await Task.Delay(100);
            throw new Exception("DEBUG: Simulated Async/Await Exception");
        }

        public void SimulateNestedException()
        {
            try
            {
                throw new InvalidOperationException("Inner Exception Detail");
            }
            catch (Exception inner)
            {
                throw new Exception("DEBUG: Simulated Nested (Outer) Exception", inner);
            }
        }

        public void SpamExceptions(int count = 100)
        {
            for (int i = 0; i < count; i++)
            {
                int index = i;
                Task.Run(() => 
                {
                    LoggingService.Instance.LogError("QA_SPAM", "StressTest", $"Spam Exception #{index}", new Exception($"Spam {index}"));
                });
            }
        }

        public void TestFallbackLogging()
        {
            // This test would normally require breaking the DB connection and then logging
            // Since we want to test the FALLBACK mechanism of LoggingService
            LoggingService.Instance.LogInfo("QA_TEST", "Fallback", "Testing fallback logging when DB is down");
        }
    }
}
#endif
