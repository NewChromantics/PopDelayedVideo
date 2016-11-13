using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class FrameBuffering : MonoBehaviour {

	public class FrameCache
	{
		public RenderTexture	Frame;
		public uint				Time;
	};

	public RenderTexture	DelayedTarget;

	[Range(1,1000)]
	public int				DelayMs = 100;

	[Range(1,50)]
	public int				MaxBufferSize;

	//	change this to a ring buffer
	public List<FrameCache>	FrameBuffer;

	private uint			LastFrameTime = 0;



	void OnNewFrame(Texture Frame,uint FrameTime)
	{
		if (FrameBuffer == null)
			FrameBuffer = new List<FrameCache> ();

		if ( FrameBuffer.Count >= MaxBufferSize )
		{
			//	delete old, or overwrite newest... a more even distribution is a complicated thing!
			FrameBuffer.RemoveRange( MaxBufferSize-1, (FrameBuffer.Count - MaxBufferSize)+1 );
		}

		var Cache = new FrameCache ();
		var NewFrame = new RenderTexture (Frame.width, Frame.height, 0, DelayedTarget.format );
		NewFrame.name = "" + FrameTime;
		Graphics.Blit (Frame, NewFrame);
		Cache.Frame = NewFrame;
		Cache.Time = FrameTime;
		FrameBuffer.Add (Cache);
	}

	void PushBuffer()
	{
		var PopMovieSimple = GetComponent<PopMovieSimple> ();
		var PopMovie = PopMovieSimple.Movie;

		uint LastCopyTime = PopMovie.GetLastFrameCopiedMs ();
		if ( LastCopyTime > LastFrameTime )
		{
			OnNewFrame (PopMovieSimple.TargetTexture,LastCopyTime);
			LastFrameTime = LastCopyTime;
		}
	}

	void PopBuffer()
	{
		//	get frame closest to the delayed time... also a complicated thing.
		var PopMovieSimple = GetComponent<PopMovieSimple> ();
		var PopMovie = PopMovieSimple.Movie;

		uint LastCopyTime = PopMovie.GetLastFrameCopiedMs ();

		//	not done an initial delay yet
		if (DelayMs > LastCopyTime)
			return;
		var DelayedTime = LastCopyTime - DelayMs;

		//	find last frame before delayed time
		int PopIndex = -1;
		for (int i = 0;	i < FrameBuffer.Count;	i++) {
			if (FrameBuffer [i].Time <= DelayedTime)
				PopIndex = i;
			else
				break;
		}
		if (PopIndex == -1)
			return;

		Graphics.Blit (FrameBuffer [PopIndex].Frame, DelayedTarget);
		FrameBuffer.RemoveRange (0, PopIndex + 1);
	}

	void Update () 
	{
		//	get new frames
		PushBuffer();
		//	push out old frames
		PopBuffer();
	}
}
