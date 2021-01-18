using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xabe.FFmpeg;

namespace GenericMediaTools.MediaDurationCalculator.Infrastructure.Services
{
    public class IOService: IIOService
    {
        public async Task CalculateVideoDuration(IEnumerable<FileInfo> files)
        {
            foreach (FileInfo file in files)
            {
                string filePath = file.FullName;

                IMediaInfo mediaInfo = await FFmpeg.GetMediaInfo(filePath);

                // we need these values to be integers (to the nearest 1000) because the StreamDeck app doesn't allow for partial seconds
                // We also add one second to the duration just so we don't transition during the end of the video
                int rawDuration = (int)Math.Ceiling(mediaInfo.Duration.TotalMilliseconds);
                int roundedDuration = (rawDuration % 1000 >= 500 ? rawDuration + 1000 - rawDuration % 1000 : rawDuration - rawDuration % 1000) + 1000;

                Console.WriteLine($"{file.Name}, {roundedDuration} ms");
            }
        }

        public IEnumerable<FileInfo> ListFilesInDirectory(string directory)
        {
            DirectoryInfo dir = new DirectoryInfo(directory);
            return dir.GetFiles();
        }

        public FileStream OpenFile(string filePath)
        {
            try
            {
                return File.OpenRead(filePath);
            }
            catch
            {
                throw;
            }
        }
    }
}