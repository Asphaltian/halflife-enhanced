using System;

namespace GoldSrc.GameUI;

public enum BackgroundState
{
	Initial,
	Menu = 2,
	Loading,
	Transition
}

public sealed class BasePanel : Panel
{
	private static readonly Color32 White = new( 255, 255, 255, 255 );
	private static readonly Color32 InGameOverlay = new( 0, 0, 0, 128 );

	private sealed class BackgroundImage
	{
		public Texture Texture;
		public int X;
		public int Y;
		public bool Scaled;
		public bool Fit;
	}

	private readonly GameUi _gameUi;
	private readonly List<BackgroundImage> _images = [];

	private int _backgroundWide;
	private int _backgroundTall;
	private BackgroundState _backgroundState;

	public BasePanel( GameUi gameUi ) : base( gameUi.Vgui, null, "BasePanel" )
	{
		_gameUi = gameUi;
	}

	public void SetBackgroundRenderState( BackgroundState state ) => _backgroundState = state;

	public override void OnChildAdded( Panel child )
	{
		if ( _gameUi.Taskbar is not null )
			PositionDialog( child );
	}

	public static void PositionDialog( Panel dialog )
	{
		var surface = dialog.Vgui.Surface;

		dialog.SetPos( (surface.ScreenWide - dialog.Wide) / 2, (surface.ScreenTall - dialog.Tall) / 2 );
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		_images.Clear();

		if ( Server.LoadMountText( ModInfo.HdBackground ? "resource/HD_BackgroundLayout.txt" : "resource/BackgroundLayout.txt" ) is not { } text )
			return;

		var position = 0;

		while ( Common.Parse( text, ref position ) is { Length: > 0 } token )
		{
			if ( token.Equals( "resolution", StringComparison.OrdinalIgnoreCase ) )
			{
				_backgroundWide = Common.Atoi( Common.Parse( text, ref position ) ?? "" );
				_backgroundTall = Common.Atoi( Common.Parse( text, ref position ) ?? "" );
				continue;
			}

			var extension = token.IndexOf( ".tga", StringComparison.Ordinal );
			var mode = Common.Parse( text, ref position ) ?? "";

			_images.Add( new BackgroundImage
			{
				Texture = Vgui.Surface.LoadImage( extension >= 0 ? token[..extension] : token ),
				Scaled = mode.Equals( "scaled", StringComparison.OrdinalIgnoreCase ),
				Fit = mode.Equals( "fit", StringComparison.OrdinalIgnoreCase ),
				X = Common.Atoi( Common.Parse( text, ref position ) ?? "" ),
				Y = Common.Atoi( Common.Parse( text, ref position ) ?? "" )
			} );
		}
	}

	public override void PaintBackground()
	{
		var surface = Vgui.Surface;

		if ( _gameUi.LevelLoaded )
		{
			surface.DrawSetColor( InGameOverlay );
			surface.DrawFilledRect( 0, 0, surface.ScreenWide, surface.ScreenTall );
			return;
		}

		if ( _backgroundState is BackgroundState.Menu or BackgroundState.Loading )
			DrawBackgroundImage();
	}

	private void DrawBackgroundImage()
	{
		if ( _backgroundWide == 0 || _backgroundTall == 0 )
			return;

		var surface = Vgui.Surface;
		var screenWide = surface.ScreenWide;
		var screenTall = surface.ScreenTall;
		var yScale = (float)screenTall / _backgroundTall;
		var xScale = (float)screenWide / _backgroundWide;
		var wide = _backgroundWide * yScale;
		var scale = yScale;

		if ( wide < screenWide )
		{
			wide = _backgroundWide * xScale;
			scale = xScale;
		}

		var xOffset = (screenWide / 2) - (int)(wide * 0.5f);
		var yOffset = (screenTall / 2) - (int)(_backgroundTall * scale * 0.5f);

		foreach ( var image in _images )
		{
			if ( image.Texture is null )
				continue;

			int x0, y0, x1, y1;

			if ( image.Scaled )
			{
				x0 = Ceil( image.X * xScale );
				y0 = Ceil( image.Y * yScale );
				x1 = Ceil( (image.Texture.Width + image.X) * xScale );
				y1 = Ceil( (image.Texture.Height + image.Y) * yScale );
			}
			else if ( image.Fit )
			{
				x0 = xOffset + Ceil( image.X * scale );
				y0 = yOffset + Ceil( image.Y * scale );
				x1 = x0 + Ceil( image.Texture.Width * scale );
				y1 = y0 + Ceil( image.Texture.Height * scale );
			}
			else
			{
				x0 = image.X;
				y0 = image.Y;
				x1 = x0 + image.Texture.Width;
				y1 = y0 + image.Texture.Height;
			}

			surface.DrawSetColor( White );
			surface.DrawTexturedRect( image.Texture, x0, y0, x1, y1 );
		}
	}

	private static int Ceil( float value ) => (int)Math.Ceiling( (double)value );
}
