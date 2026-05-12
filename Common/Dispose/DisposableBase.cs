using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.Dispose
{
    public abstract class DisposableBase : IDisposable
    {
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    DisposeManaged();

                DisposeUnmanaged();
                _disposed = true;
            }
        }
        protected virtual void DisposeManaged() { }
        protected virtual void DisposeUnmanaged() { }

        ~DisposableBase()
        {
            Dispose(false);
        }
    }
}
