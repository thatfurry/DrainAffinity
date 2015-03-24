namespace DrainAffinity
{
    internal sealed class WorkResult
    {
        public WorkResult(WorkRequestAction action, bool success)
        {
            this.Action = action;
            this.Success = success;
        }

        public WorkRequestAction Action { get; private set; }

        public bool Success { get; private set; }
        
        public WorkRequest[] Subtasks { get; set; }
    }
}
