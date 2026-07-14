using System;
using System.Collections.Generic;

namespace UmdEssentials.PanoptoCloud
{
    public class DefaultRecordingFolder
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class RecorderInfo
    {
        public RemoteRecorderState State { get; set; }
        public Guid Id { get; set; }
        public string Name { get; set; }
        public DefaultRecordingFolder DefaultRecordingFolder { get; set; }

        public RecorderInfo()
        {
            DefaultRecordingFolder = new DefaultRecordingFolder();
            State = RemoteRecorderState.Unknown;
        }
    }

    public class RemoteRecorderSearchResult
    {
        public List<RecorderInfo> Results { get; set; }
    }
}