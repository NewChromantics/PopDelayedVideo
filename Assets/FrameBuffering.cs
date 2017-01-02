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

	[Header("video input. If PopMovieObject is null, a webcam texture is created with the name below")]
	public PopMovieSimple	PopMovieObject;
	public string			WebcamName;
	public WebCamTexture	Webcam;

	[Header("Delay before enabling live feed")]
	[Range(1,10)]
	public float			DelayBeforeShowLiveFeed = 4;

	[Header("Left eye contains live feed when this is checked")]
	public bool				ShowLiveFeed = false;

	[Header("Delay from live feed in seconds")]
	[Range(1,5)]
	public float			DelaySeconds = 0.1f;

	[Range(1,50)]
	public int				MaxBufferSize;

	//	change this to a ring buffer
	public List<FrameCache>	FrameBuffer;

	private uint			LastFrameTime = 0;
	private float			WebcamFirstFrameTime = 0;

	[Header("How often do we strobe")]
	[Range(0.1f,10)]
	public float			StrobeDelay = 2;
	private float			StrobeTimeout = 0;

	[Header("How long does the strobe last")]
	[Range(0.01f,1.0f)]
	public float			StrobeDuration = 0.1f;

	[Header("Optionally set a texture to show when strobing (Colour used if no texture)")]
	public Texture 			StrobeTexture = null;
	public Color			StrobeColour = new Color (0, 0, 0, 1);

	[Header("Clear targets at start to this colour")]
	public bool				InitialClear = true;
	public Color			InitialClearColour = new Color (0, 0, 0, 1);

	void Start()
	{
		//	make a plain black texture. We would use Texture2D.blacktexture, but it has a 0 alpha
		if ( StrobeTexture == null )
		{
			var StrobeTexture2D = new Texture2D(1,1,TextureFormat.RGB24,true);
			StrobeTexture = StrobeTexture2D;
			StrobeTexture2D.SetPixel (0, 0, StrobeColour);
			StrobeTexture2D.Apply ();
		}

		//	clear targets at startup
		if (InitialClear) 
		{
			var ClearTexture = new Texture2D (1, 1, TextureFormat.RGB24, true);
			ClearTexture.SetPixel (0, 0, InitialClearColour);
			ClearTexture.Apply ();
			Graphics.Blit (ClearTexture, LeftEye);
			Graphics.Blit (ClearTexture, RightEye);
		}

		if (PopMovieObject == null)
		{
			PopMovieObject = GetComponent<PopMovieSimple> ();
		}
	}

	void OnNewFrame(Texture Frame,uint FrameTime)
	{
		//Debug.Log("New frame: " + FrameTime);

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

	void PushBuffer_PopMovie()
	{
		var PopMovie = GetMovie();
		if (PopMovie == null) {
			Debug.Log ("Waiting for movie");
			return;
		}

		uint LastCopyTime = PopMovie.GetLastFrameCopiedMs ();
		if ( LastCopyTime > LastFrameTime )
		{
			OnNewFrame (PopMovieObject.TargetTexture,LastCopyTime);
			LastFrameTime = LastCopyTime;
		}
	}

	uint GetWebcamTime()
	{
		if ( WebcamFirstFrameTime == 0 )
			WebcamFirstFrameTime = Time.time;

		var TimeMs = (uint)((Time.time - WebcamFirstFrameTime) * 1000.0f);
		return TimeMs;
	}


	void PushBuffer_Webcam()
	{
		if (Webcam == null)
		{
			Webcam = new WebCamTexture( WebcamName );
			Webcam.Play();
		}

		if (Webcam.didUpdateThisFrame)
		{
			uint LastCopyTime = GetWebcamTime();
			if ( LastCopyTime > LastFrameTime )
			{
				OnNewFrame (Webcam,LastCopyTime);
				LastFrameTime = LastCopyTime;
			}
		}

	}

	
	PopMovie GetMovie()
	{
		if (!PopMovieObject)
			return null;
		var Movie = PopMovieObject.Movie;
		return Movie;
	}

	void PopBuffer()
	{
		//	copy live frame
		if (ShowLiveFeed) {
			if (PopMovieObject)
			{
				Graphics.Blit(PopMovieObject.TargetTexture, LeftEye);
			}
			else if (Webcam)
			{
				Graphics.Blit(Webcam, LeftEye);
			}

		}

		//uint LastCopyTime = PopMovie.GetLastFrameCopiedMs ();
		uint LastCopyTime = LastFrameTime;
		uint DelayMs = (uint)(DelaySeconds * 1000);

		//	not done an initial delay yet
		if (DelayMs > LastCopyTime)
			return;

		//	gr: don't strobe until we've buffered, otherwise the strobe can sit on screen too long.
		//	check in case we need to strobe
		StrobeTimeout -= Time.unscaledDeltaTime;
		//	when counter is negative, we use it as the "how long we're strobing for" counter
		if (StrobeTimeout < -StrobeDuration) {
			//	reset 
			//	gr: +Timeout so StrobeDelay is the time until next starts, not the GAP between strobes
			StrobeTimeout = StrobeDelay + StrobeTimeout;
		} else if (StrobeTimeout <= 0) {
			//	strobe
			//	gr: could save GPU time here and skip blit if we know the texture is black from before
			Graphics.Blit (StrobeTexture, LeftEye);
			Graphics.Blit (StrobeTexture, RightEye);
			return;
		}
		else
		{
			//	counting down to strobe
		}



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
		if ( PopMovieObject != null )
		{
			PushBuffer_PopMovie();
		}
		else
		{
			PushBuffer_Webcam();
		}
		

		DoLiveSwitch ();

		//	push out old frames
		PopBuffer();
	}
}
