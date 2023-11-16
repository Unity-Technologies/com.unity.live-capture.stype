using UnityEngine;

namespace Unity.LiveCapture.Stype.Tests.Runtime
{
    internal class WaitUntilWithTimeout : CustomYieldInstruction
    {
        public bool IsCompleted { get; private set; }

        private readonly float timeoutTime;

        private readonly System.Func<bool> predicate;

        public override bool keepWaiting
        {
            get
            {
                IsCompleted = predicate();
                if (IsCompleted)
                {
                    return false;
                }

                return !(Time.realtimeSinceStartup >= timeoutTime);
            }
        }

        public WaitUntilWithTimeout(System.Func<bool> predicate, float timeoutSecond)
        {
            this.timeoutTime = Time.realtimeSinceStartup + timeoutSecond;
            this.predicate = predicate;
        }
    }
}
