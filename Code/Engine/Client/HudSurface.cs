using Sandbox.Rendering;
using Sandbox.UI;

namespace GoldSrc;

public sealed class HudSurface : Panel
{
	public Client Client { get; set; }

	public override void Tick()
	{
		var cursor = Client is { GameUiActive: true } ? Client.GameUiCursor : Client is { VGuiCursorVisible: true } ? Client.VGuiCursor : null;

		if ( Style.Cursor != cursor )
			Style.Cursor = cursor;

		var wantsFocus = Client is { MessageMode: true } or { GameUiActive: true };

		if ( wantsFocus == AcceptsFocus )
			return;

		AcceptsFocus = wantsFocus;

		if ( wantsFocus )
			Focus();
		else
			Blur();
	}

	public override void OnKeyTyped( char k )
	{
		if ( Client is { GameUiActive: true } )
			Client.GameUiCharTyped( k );
		else
			Client?.KeyMessageText( k.ToString() );
	}

	public override void OnPaste( string text )
	{
		if ( Client is { GameUiActive: true } )
			Client.GameUiPaste( text );
		else
			base.OnPaste( text );
	}

	public override string GetClipboardValue( bool cut )
	{
		if ( Client is not { GameUiActive: true } )
			return base.GetClipboardValue( cut );

		Client.GameUiCopy( cut );

		return null;
	}

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( Client is { GameUiActive: true } )
			Client.GameUiKeyDown( e.Button, e.KeyboardModifiers );
		else
			Client?.KeyMessage( e.Button );

		e.StopPropagation = true;
	}

	public override void OnButtonEvent( ButtonEvent e )
	{
		if ( Client is not { GameUiActive: true } )
		{
			base.OnButtonEvent( e );
			return;
		}

		if ( !e.Pressed )
			Client.GameUiKeyUp( e.Button );

		e.StopPropagation = true;
	}

	protected override void OnMouseDown( MousePanelEvent e )
	{
		Client?.GameUiMousePressed( e.Button );
		Client?.VGuiMousePressed( e.Button );
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		Client?.GameUiMouseReleased( e.Button );
		Client?.VGuiMouseReleased( e.Button );
	}

	public override void OnMouseWheel( Vector2 value )
	{
		Client?.GameUiMouseWheel( (int)-value.y );
		Client?.VGuiMouseWheel( (int)-value.y );
	}

	public override void OnDraw( Painter painter )
	{
		if ( Client is null )
			return;

		foreach ( var draw in Client.HudDraws )
		{
			using var scope = painter.Scope();

			if ( draw.Scissor is { } scissor )
				painter.Clip( new Rect( scissor.Left, scissor.Top, scissor.Right - scissor.Left, scissor.Bottom - scissor.Top ) );

			painter.BlendMode = draw.Mode switch
			{
				HudDrawMode.Additive or HudDrawMode.Fill => BlendMode.Lighten,
				HudDrawMode.Modulate => BlendMode.Multiply,
				_ => BlendMode.Normal
			};

			if ( draw.Texture is null )
			{
				painter.Fill = Fill.Solid( draw.Color );
				painter.Clip( new Rect( draw.X, draw.Y, draw.Width, draw.Height ) );
				painter.Rect( new Rect( draw.X - 1, draw.Y - 1, draw.Width + 2, draw.Height + 2 ) );
				continue;
			}

			if ( draw.Mode == HudDrawMode.StretchNearest )
				painter.Fill = Fill.Image( draw.Texture, draw.Color, Length.Pixels( draw.Width ), Length.Pixels( draw.Height ), Length.Pixels( 0 ), Length.Pixels( 0 ), BackgroundRepeat.Clamp, FilterMode.Point );
			else if ( draw.Mode == HudDrawMode.Stretch )
				painter.Fill = Fill.Image( draw.Texture, draw.Color, Length.Pixels( draw.Width ), Length.Pixels( draw.Height ), Length.Pixels( 0 ), Length.Pixels( 0 ), BackgroundRepeat.Clamp, FilterMode.Bilinear );
			else
				painter.Fill = Fill.Image( draw.Texture, draw.Color, Length.Pixels( draw.Texture.Width ), Length.Pixels( draw.Texture.Height ), Length.Pixels( -draw.Left ), Length.Pixels( -draw.Top ), BackgroundRepeat.Clamp, FilterMode.Point );

			painter.Rect( new Rect( draw.X, draw.Y, draw.Width, draw.Height ) );
		}
	}
}
