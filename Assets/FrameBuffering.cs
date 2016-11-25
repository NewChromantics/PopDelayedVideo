using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class FrameBuffering : MonoBehaviour {

	public class FrameCache
	{
		public RenderTexture	Frame;
		public uint				Time;
	};

	public RenderTexture	LeftEye;
	public RenderTexture	RightEye;

	[Header("Delay before enabling live feed")]
	[Range(1,10)]
	public float			DelayBeforeShowLiveFeed = 4;

	public bool				ShowLiveFeed = false;

	[Header("Delay from live feed in seconds")]
	[Range(1,5)]
	public float			DelaySeconds = 0.1f;

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
		var NewFrame = new RenderTexture (Frame.width, Frame.height, 0, RightEye.format );
		NewFrame.name = "" + FrameTime;
		Graphics.Blit (Frame, NewFrame);
		Cache.Frame = NewFrame;
		Cache.Time = FrameTime;
		FrameBuffer.Add (Cache);
	}

	void PushBuffer()
	{
		var PopMovie = GetMovie();
		if (PopMovie == null) {
			Debug.Log ("Waiting for movie");
			return;
		}

		uint LastCopyTime = PopMovie.GetLastFrameCopiedMs ();
		if ( LastCopyTime > LastFrameTime )
		{
			var PopMovieSimple = GetComponent<PopMovieSimple> ();
			OnNewFrame (PopMovieSimple.TargetTexture,LastCopyTime);
			LastFrameTime = LastCopyTime;
		}
	}

	PopMovie GetMovie()
	{
		var PopMovieSimple = GetComponent<PopMovieSimple> ();
		var Movie = PopMovieSimple.Movie;
		return Movie;
	}

	void PopBuffer()
	{
		//	get frame closest to the delayed time... also a complicated thing.
		var PopMovie = GetMovie();
		if (PopMovie == null)
			return;

		uint LastCopyTime = PopMovie.GetLastFrameCopiedMs ();
		uint DelayMs = (uint)(DelaySeconds * 1000);

		//	copy live frame
		if (ShowLiveFeed) {
			var PopMovieSimple = GetComponent<PopMovieSimple> ();
			Graphics.Blit (PopMovieSimple.TargetTexture, LeftEye);
		}

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

		Graphics.Blit (FrameBuffer [PopIndex].Frame, RightEye);
		if( !ShowLiveFeed )
			Graphics.Blit (FrameBuffer [PopIndex].Frame, LeftEye);
		
		FrameBuffer.RemoveRange (0, PopIndex + 1);
	}

	void DoLiveSwitch()
	{
		if (ShowLiveFeed)
			return;
		
		var PopMovie = GetMovie();
		if (PopMovie == null)
			return;

		float LastCopyTime = PopMovie.GetLastFrameCopied ();
		if (LastCopyTime > DelayBeforeShowLiveFeed)
			ShowLiveFeed = true;
	}

	void Update () 
	{
		//	get new frames
		PushBuffer();

		DoLiveSwitch ();

		//	push out old frames
		PopBuffer();
	}
}
