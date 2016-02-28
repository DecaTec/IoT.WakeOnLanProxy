using Windows.ApplicationModel.Background;
using Windows.Foundation;
using Windows.System.Threading;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace DecaTec.IoT.WakeOnLanProxy
{
    public sealed class WolProxyStartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral backgroundTaskDeferral;
        private WolProxy wolProxy;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // Get the deferral and save it to local variable so that the app stays alive.
            this.backgroundTaskDeferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            IAsyncAction asyncAction = ThreadPool.RunAsync((handler) =>
            {
                this.wolProxy = new WolProxy(9, 9);
                this.wolProxy.Start();
            });
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (this.wolProxy != null)
            {
                this.wolProxy.Stop();
            }

            // Release the deferral so that the app can be stopped.
            this.backgroundTaskDeferral.Complete();
        }
    }
}
