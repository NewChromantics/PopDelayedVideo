//#define ENABLE_POPMOVIE
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
#if ENABLE_POPMOVIE
	public PopMovieSimple	PopMovieObject;
#endif
	public string			WebcamName;
	public WebCamTexture	Webcam;

	[Header("Set this texture and we'll use it instead of a webcam (for when you have no webcam)")]
	public Texture			DummyWebcam;

	[Header("Delay before enabling live feed")]
	[Range(1,10)]
	public float			DelayBeforeShowLiveFeed = 4;

	[Header("Left eye contains live feed when this is checked")]
	public bool				ShowLiveFeed = false;

	[Header("Delay from live feed in seconds")]
	[Range(1,10)]
	public float			DelaySeconds = 0.1f;

	[Range(1,50)]
	public int				MaxBufferSize;

	//	change this to a ring buffer
	public List<FrameCache>	FrameBuffer;

	private uint			LastFrameTime = 0;
	private float			WebcamFirstFrameTime = 0;

	public bool				StrobeEnabled = true;

	[Header("How long does the strobe last (seconds)")]
	[Range(0.01f,7.0f)]
	public float			_StrobeDuration = 0.1f;
	float					StrobeDuration			{	get { return StrobeEnabled ? _StrobeDuration : 0; } }
	[Header("How long does video (non-strobe) last (seconds)")]
	[Range(0.01f,7.0f)]
	public float			NonStrobeDuration = 0.1f;
	private float			StrobeTimer = 0;


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

#if ENABLE_POPMOVIE
		if (PopMovieObject == null)
		{
			PopMovieObject = GetComponent<PopMovieSimple> ();
		}
#endif
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

		LastFrameTime = FrameTime;
	}

#if ENABLE_POPMOVIE
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
		}
	}
#endif

	uint GetWebcamTime()
	{
		if ( WebcamFirstFrameTime == 0 )
			WebcamFirstFrameTime = Time.time;

		var TimeMs = (uint)((Time.time - WebcamFirstFrameTime) * 1000.0f);
		return TimeMs;
	}


	void PushBuffer_Webcam()
	{
		if (Webcam == null && DummyWebcam == null)
		{
			try
			{
				Webcam = new WebCamTexture( WebcamName );
				Webcam.Play();
				if ( !Webcam.isPlaying )
					throw new System.Exception("No webcam");
			}
			catch {
				Webcam = null;
				DummyWebcam = Texture2D.whiteTexture;
			}
		}

		if ( DummyWebcam != null || (Webcam!=null && Webcam.didUpdateThisFrame) )
		{
			uint LastCopyTime = GetWebcamTime();
			if ( LastCopyTime > LastFrameTime )
			{
				OnNewFrame ( DummyWebcam ? DummyWebcam : Webcam,LastCopyTime);
			}
		}

	}

#if ENABLE_POPMOVIE
	PopMovie GetMovie()
	{
		if (!PopMovieObject)
			return null;
		var Movie = PopMovieObject.Movie;
		return Movie;
	}
#endif

	void PopBuffer()
	{
		//	copy live frame
		if (ShowLiveFeed)
		{
#if ENABLE_POPMOVIE
			if (PopMovieObject) 
			{
				Graphics.Blit (PopMovieObject.TargetTexture, LeftEye);
			}
			else
#endif 
			if (DummyWebcam) 
			{
				Graphics.Blit(DummyWebcam, LeftEye);
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


		StrobeTimer += Time.unscaledDeltaTime;
		float TotalDuration = StrobeDuration + NonStrobeDuration;
		StrobeTimer = StrobeTimer % TotalDuration;

		if (StrobeTimer < StrobeDuration) {
			//	strobing
			Graphics.Blit (StrobeTexture, LeftEye);
			Graphics.Blit (StrobeTexture, RightEye);
			return;
		}

		//	else we're showing video


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

#if ENABLE_POPMOVIE
		var PopMovie = GetMovie();
		if (PopMovie == null)
			return;

		float LastCopyTime = PopMovie.GetLastFrameCopied ();
		if (LastCopyTime > DelayBeforeShowLiveFeed)
			ShowLiveFeed = true;
#endif
	}

	void Update () 
	{
		//	get new frames
#if ENABLE_POPMOVIE
		if ( PopMovieObject != null )
		{
			PushBuffer_PopMovie();
		}
		else
#endif
		{
			PushBuffer_Webcam();
		}
		

		DoLiveSwitch ();

		//	push out old frames
		PopBuffer();
	}
}
