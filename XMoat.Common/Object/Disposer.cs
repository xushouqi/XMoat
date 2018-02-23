using System;

namespace XMoat.Common
{
    public abstract class Disposer : Object, IDisposable
    {
        public long Id { get; set; }

        public bool IsFromPool { get; set; }

        private Action<Disposer> mOnDispose;

        protected Disposer()
        {
            this.Id = IdGenerater.GenerateId32();
        }

        protected Disposer(long id)
        {
            this.Id = id;
        }

        public void SetDisposeAction(Action<Disposer> onDispose)
        {
            mOnDispose = onDispose;
        }

        public virtual void Dispose()
        {
            this.Id = 0;
            if (this.IsFromPool)
            {
                if (mOnDispose != null) mOnDispose(this);
            }
        }
    }
}