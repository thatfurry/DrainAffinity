namespace DrainAffinity
{
    internal sealed class WorkRequest
    {
        public WorkRequest(string target, WorkRequestAction action)
        {
            this.Target = target;
            this.Action = action;
        }

        public WorkRequestAction Action { get; private set; }

        public string Target { get; private set; }

        public string Url { get; set; }

        public int? Page { get; set; }
    }
}
