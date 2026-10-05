using System;
using System.Collections.ObjectModel;

namespace Tang.VideoThrimalgoAI.Models
{
    public class ProcessingTask
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TaskName { get; set; } = string.Empty;
        public ProcessingType Type { get; set; }
        public ObservableCollection<string> InputFiles { get; set; } = new ObservableCollection<string>();
        public string OutputDirectory { get; set; } = string.Empty;
        public TaskStatus Status { get; set; } = TaskStatus.Pending;
        public double Progress { get; set; }
        public int ProcessedFiles { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public enum ProcessingType
    {
        ImageEnhancement,
        SuperResolution,
        MosaicRemoval,
        WatermarkRemoval,
        VideoRepair
    }

    public enum TaskStatus
    {
        Pending,
        Running,
        Completed,
        Failed,
        Cancelled
    }
}
