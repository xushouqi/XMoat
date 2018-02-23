using System;
using System.Collections.Generic;

namespace XMoat.Common
{
    public class ObjectPool
    {
        private readonly Dictionary<Type, Queue<Disposer>> dictionary = new Dictionary<Type, Queue<Disposer>>();

        public T Fetch<T>() where T : Disposer
        {
            T t = (T)this.Fetch(typeof(T));
            t.IsFromPool = true;
            return t;
        }

        private Disposer Fetch(Type type)
        {
            Queue<Disposer> queue;
            if (!this.dictionary.TryGetValue(type, out queue))
            {
                queue = new Queue<Disposer>();
                this.dictionary.Add(type, queue);
            }
            Disposer obj;
            //从队列中获取
            if (queue.Count > 0)
            {
                obj = queue.Dequeue();
                obj.Id = IdGenerater.GenerateId32();
            }
            else
            {
                obj = (Disposer)Activator.CreateInstance(type);
                //设置对象销毁时的回收函数
                obj.SetDisposeAction(OnRecycle);
            }
            return obj;
        }

        private void OnRecycle(Disposer obj)
        {
            Recycle(obj);
        }

        public void Recycle(Disposer obj)
        {
            Type type = obj.GetType();
            Queue<Disposer> queue;
            if (!this.dictionary.TryGetValue(type, out queue))
            {
                queue = new Queue<Disposer>();
                this.dictionary.Add(type, queue);
            }
            queue.Enqueue(obj);
        }
    }
}