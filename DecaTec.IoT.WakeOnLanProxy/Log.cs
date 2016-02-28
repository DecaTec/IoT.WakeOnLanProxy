using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DecaTec.IoT.WakeOnLanProxy
{
    public static class Log
    {
        private static string LogFileName = @"WakeOnLanProxyLog.txt";
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1);

        public async static void WriteLog(string message)
        {
            await semaphore.WaitAsync();

            IRandomAccessStream raStream = null;
            IInputStream inputStream = null;
            IOutputStream outputStream = null;

            try
            {
                var localFolder = ApplicationData.Current.LocalFolder;
                var logFile = await localFolder.CreateFileAsync(LogFileName, CreationCollisionOption.OpenIfExists);

                using (raStream = await logFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var sBuilder = new StringBuilder();
                    inputStream = raStream.GetInputStreamAt(0);
                    var streamReader = new StreamReader(inputStream.AsStreamForRead());
                    var fileContent = await streamReader.ReadToEndAsync();
                    inputStream.Dispose();
                    inputStream = null;
                    var sArr = fileContent.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    Array.Reverse(sArr);

                    for (int i = 0; i < 100; i++)
                    {
                        if (i >= sArr.Length)
                            break;

                        sBuilder.Append(sArr[i] + Environment.NewLine);
                    }

                    var newContent = sBuilder.ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    Array.Reverse(newContent);

                    var timeStr = DateTime.Now.ToString(CultureInfo.CurrentCulture);
                    outputStream = raStream.GetOutputStreamAt(0);
                    var dataWriter = new DataWriter(outputStream);

                    foreach (var c in newContent)
                    {
                        dataWriter.WriteString(c + Environment.NewLine);
                    }

                    dataWriter.WriteString(timeStr + ": " + message + Environment.NewLine);
                    await dataWriter.StoreAsync();
                    dataWriter.DetachStream();
                    await outputStream.FlushAsync();
                    outputStream.Dispose();
                    outputStream = null;
                    raStream.Dispose();
                    raStream = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception in Log.WriteLog: " + ex.Message);
            }
            finally
            {
                semaphore.Release();

                if (inputStream != null)
                {
                    inputStream.Dispose();
                    inputStream = null;
                }

                if (outputStream != null)
                {
                    outputStream.Dispose();
                    outputStream = null;
                }

                if (raStream != null)
                {
                    raStream.Dispose();
                    raStream = null;
                }
            }
        }
    }
}
