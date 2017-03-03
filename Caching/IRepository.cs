using System;

namespace TFLLib.Caching
{
   public interface IRepository : IDisposable
   {
      bool IsDisposed { get; }
   }
}
