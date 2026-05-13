#if DEBUG
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using QuanLyGiuXe.DebugTools.Models;
using QuanLyGiuXe.Services;

namespace QuanLyGiuXe.DebugTools.Services
{
    public class QaTestService
    {
        private static readonly Lazy<QaTestService> _instance = new Lazy<QaTestService>(() => new QaTestService());
        public static QaTestService Instance => _instance.Value;

        public ObservableCollection<TestResult> Results { get; } = new ObservableCollection<TestResult>();

        private QaTestService() { }

        public async Task<TestResult> RunTestAsync(string name, string category, Func<Task> testAction, CancellationToken ct = default)
        {
            var result = new TestResult { TestName = name, Category = category, Timestamp = DateTime.Now };
            var sw = Stopwatch.StartNew();

            try
            {
                await testAction();
                result.Success = true;
                LoggingService.Instance.LogAudit("QA_PASS", "System", name, null, null, details: $"Test {name} passed in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                LoggingService.Instance.LogError("QA_FAIL", category, $"Test {name} failed", ex);
            }
            finally
            {
                sw.Stop();
                result.Duration = sw.Elapsed;
                
                // Add to results on UI thread
                System.Windows.Application.Current?.Dispatcher?.Invoke(() => 
                {
                    Results.Insert(0, result);
                });
            }

            return result;
        }

        public void ClearResults()
        {
            Results.Clear();
        }
    }
}
#endif
