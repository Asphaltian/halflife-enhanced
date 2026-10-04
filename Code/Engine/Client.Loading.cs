using System;
using System.Threading.Tasks;
using Sandbox.Rendering;

namespace GoldSrc;

partial class Client
{
	private const byte LoadingTransparentIndex = 255;
	private const int LoadingPicHeaderSize = 8;
	private const int LoadingPicPaletteCountSize = 2;

	private bool _drawLoading;
	private bool _holdLoadingFrame;
	private Texture _loadingPic;
	private Texture _loadingFrame;
	private CommandList _loadingGrab;
	private int _frameCount;
	private int _loadingGrabFrame;
	private TaskCompletionSource _loadingPresented;

	public void BeginLoadingPlaque()
	{
		Sound?.StopAllSounds();

		if ( _state is not (ClientState.Connected or ClientState.Uninitialized or ClientState.Active) || _signon != SignOns )
			return;

		_centerTimeOff = 0f;

		if ( !_drawLoading )
		{
			_drawLoading = true;

			GrabLoadingFrame();
			DrawLoading();
		}

		_gameUi?.LoadingStarted( "transition" );
	}

	public Task LoadingPlaquePresented()
	{
		if ( !_drawLoading )
			return Task.CompletedTask;

		_loadingPresented ??= new TaskCompletionSource();

		return _loadingPresented.Task;
	}

	private void LoadingFramePresented()
	{
		ReleaseLoadingGrab();

		var presented = _loadingPresented;

		_loadingPresented = null;

		presented?.SetResult();
	}

	public void EndLoadingPlaque()
	{
		if ( !_drawLoading )
			return;

		_drawLoading = false;

		ReleaseLoadingGrab();

		if ( InLevel )
			ReleaseLoadingFrame();
		else
			_holdLoadingFrame = true;

		_gameUi?.LoadingFinished();

		HideGameUi();
	}

	private void ReleaseLoadingFrame()
	{
		_holdLoadingFrame = false;
		_hudDraws.Clear();

		_loadingFrame?.Dispose();
		_loadingFrame = null;
	}

	private void GrabLoadingFrame()
	{
		if ( Scene.Camera is not { } camera )
			return;

		_holdLoadingFrame = false;
		_loadingFrame?.Dispose();
		_loadingFrame = Texture.CreateRenderTarget( "loading", ImageFormat.RGBA16161616F, new Vector2( ScreenWidth, ScreenHeight ) );

		_loadingGrabFrame = _frameCount;
		_loadingGrab = new CommandList( "LoadingPlaque" );
		_loadingGrab.Attributes.GrabFrameTexture( "ColorBuffer" );
		_loadingGrab.SetRenderTarget( RenderTarget.From( _loadingFrame ) );
		_loadingGrab.Blit( Material.FromShader( "shaders/postprocess/frame_copy.shader" ) );
		_loadingGrab.ClearRenderTarget();

		camera.AddCommandList( _loadingGrab, Sandbox.Rendering.Stage.AfterPostProcess );

		_hudDraws.Insert( 0, new HudDraw( HudDrawMode.Normal, _loadingFrame, 0, 0, ScreenWidth, ScreenHeight, 0, 0, Color32.White, null ) );
	}

	private void ReleaseLoadingGrab()
	{
		if ( _loadingGrab is null )
			return;

		Scene.Camera?.RemoveCommandList( _loadingGrab );

		_loadingGrab = null;
	}

	private void DrawLoading()
	{
		_loadingPic ??= LoadLoadingPic();

		if ( _loadingPic is null )
			return;

		_hudDraws.Add( new HudDraw( HudDrawMode.Holes, _loadingPic, (ScreenWidth / 2) - (_loadingPic.Width / 2), (ScreenHeight / 2) - (_loadingPic.Height / 2), _loadingPic.Width, _loadingPic.Height, 0, 0, Color32.White, null ) );
	}

	private static Texture LoadLoadingPic()
	{
		if ( Server.LoadMountFile( "gfx.wad/lambda" ) is not { } data )
			return null;

		var width = BitConverter.ToInt32( data, 0 );
		var height = BitConverter.ToInt32( data, 4 );
		var palette = LoadingPicHeaderSize + (width * height) + LoadingPicPaletteCountSize;
		var pixels = new byte[width * height * 4];

		for ( var i = 0; i < width * height; i++ )
		{
			var index = data[LoadingPicHeaderSize + i];

			pixels[(i * 4) + 0] = data[palette + (index * 3) + 0];
			pixels[(i * 4) + 1] = data[palette + (index * 3) + 1];
			pixels[(i * 4) + 2] = data[palette + (index * 3) + 2];
			pixels[(i * 4) + 3] = index == LoadingTransparentIndex ? (byte)0 : (byte)255;
		}

		return Texture.Create( width, height ).WithData( pixels ).Finish();
	}
}
