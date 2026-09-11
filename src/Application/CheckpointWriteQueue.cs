using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Earthward.Domain;
namespace Earthward.Application;

/// <summary>Single serial worker for owned, prevalidated snapshots. Never accesses scene or live state.</summary>
public sealed class CheckpointWriteQueue
{
    public sealed record Result(long Sequence,string RunId,long Wave,bool IsWaveStart,bool MainSaved,bool WaveSaved,string Error);
    private sealed record Request(long Sequence,string RunId,long Wave,string MainPath,string? WavePath,DataMap Snapshot,DataMap? WaveRecord);
    private readonly object _gate=new();
    private readonly Queue<Result> _completed=new();
    private Request? _pendingWave,_pendingOrdinary;
    private Task? _worker;
    private bool _running;
    private long _sequence;
    public bool IsBusy { get { lock(_gate)return _running; } }
    public long EnqueueOwned(string runId,long wave,string mainPath,DataMap ownedValidatedSnapshot,string? wavePath=null,DataMap? ownedWaveRecord=null)
    {
        if((wavePath==null)!=(ownedWaveRecord==null))throw new ArgumentException("Wave path and record must be supplied together.");
        lock(_gate)
        {
            var request=new Request(++_sequence,runId,wave,mainPath,wavePath,ownedValidatedSnapshot,ownedWaveRecord);
            if(ownedWaveRecord!=null){_pendingWave=request;_pendingOrdinary=null;}
            else _pendingOrdinary=request;
            if(!_running){_running=true;_worker=Task.Run(Drain);}
            return request.Sequence;
        }
    }
    private void Drain()
    {
        while(true)
        {
            Request? request;
            lock(_gate)
            {
                request=_pendingWave??_pendingOrdinary;
                if(request==null){_running=false;return;}
                if(ReferenceEquals(request,_pendingWave))_pendingWave=null;else _pendingOrdinary=null;
            }
            bool waveSaved=false,mainSaved=false;string error="";
            try
            {
                var bytes=CheckpointFile.Serialize(request.Snapshot);
                if(request.WaveRecord!=null)waveSaved=CheckpointFile.WriteVerified(request.WavePath!,CheckpointFile.WrapWaveStart(request.WaveRecord,bytes));
                if(request.WaveRecord==null||waveSaved)mainSaved=CheckpointFile.WriteVerified(request.MainPath,bytes);
                else error="Wave checkpoint byte verification failed; previous files are preserved.";
            }
            catch(Exception exception){error=exception.Message;}
            lock(_gate)_completed.Enqueue(new(request.Sequence,request.RunId,request.Wave,request.WaveRecord!=null,mainSaved,waveSaved,error));
        }
    }
    public IReadOnlyList<Result> TakeResults()
    {
        lock(_gate){var result=_completed.ToArray();_completed.Clear();return result;}
    }
    public void Flush()
    {
        while(true)
        {
            Task? worker;lock(_gate){worker=_worker;if(!_running)return;}
            worker?.GetAwaiter().GetResult();
        }
    }
}
