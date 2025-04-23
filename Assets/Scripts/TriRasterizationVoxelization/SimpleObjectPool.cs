using System.Collections.Generic;

namespace TriRasterizationVoxelization
{
    public class SpanPool : ObjectPool<HeightFieldSpan>
    {
        public void Clear()
        {
        }
    }
    
    public class ObjectPool<T> where T : IClear, new()
    {
        public T Gen()
        {
            if (_pool.Count > 0)
                return _pool.Pop();
            
            _pool = new Stack<T>(_initCount << 1);
            return new T();
        }
        
        public void Release(T obj)
        {
            if (obj is null)
                return;
            
            obj.Clear();
            _pool.Push(obj);
        }

        public void Clear()
        {
            _pool.Clear();
        }

        private Stack<T> _pool = new Stack<T>(1 << 8);
        private int _initCount = 1 << 8;
    }

    public interface IClear
    {
        public void Clear();
    }
}