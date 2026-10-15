#nullable enable
using GCMod.Nextgen;
namespace GCMod.Candidate.Native;
/// Managed tokens are checked before every native read, including identity/glyph/geometry.
public sealed class GameThreadGate {
 int main; bool alive=true; long serial;
 public bool IsMainThread => main!=0 && Environment.CurrentManagedThreadId==main;
 public bool IsAlive => alive;
 public bool CanAccess => IsAlive&&IsMainThread;
 public void Capture(){if(main==0)main=Environment.CurrentManagedThreadId;else if(!IsMainThread)throw new InvalidOperationException("MAIN_THREAD_CHANGED");}
 public void Close()=>alive=false;
 public long NextLifetime()=>Interlocked.Increment(ref serial);
 public void Require(){if(!CanAccess)throw new InvalidOperationException("NATIVE_ACCESS_GATE_CLOSED");}
}
public sealed class MainThreadScheduler(GameThreadGate gate):IRuntimeMainThreadScheduler {
 readonly System.Collections.Concurrent.ConcurrentQueue<Action> queue=new();
 public bool IsMainThread=>gate.CanAccess;
 public Task<T> InvokeAsync<T>(Func<T> action,CancellationToken cancel){
  if(IsMainThread){cancel.ThrowIfCancellationRequested();return Task.FromResult(action());}
  var done=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
  var registration=cancel.Register(()=>done.TrySetCanceled(cancel));_ = done.Task.ContinueWith(_=>registration.Dispose(),CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
  queue.Enqueue(()=>{if(cancel.IsCancellationRequested){done.TrySetCanceled(cancel);return;}try{done.TrySetResult(action());}catch(Exception e){done.TrySetException(e);}});return done.Task;
 }
 public void Tick(){gate.Require();while(queue.TryDequeue(out var action))action();}
}
