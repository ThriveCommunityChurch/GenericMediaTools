using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MediaDurationCalculator.Infrastructure.Services
{
    interface IIOService
    {
        public FileStream OpenFile(string filePath);

        public IEnumerable<FileInfo> ListFilesInDirectory(string directory);

        public Task CalculateVideoDuration(IEnumerable<FileInfo> files);
    }
}