using System;

namespace GoldSrc.GameUI;

public class MessageBox : Frame
{
	private readonly Label _messageLabel;

	private Panel _frameOver;

	protected Button OkButton { get; }

	public MessageBox( Vgui vgui, string title, string text, Panel parent ) : base( vgui, parent, null )
	{
		SetTitle( title );

		_messageLabel = new Label( vgui, this, null, text );

		SetMenuButtonResponsive( false );
		SetCloseButtonVisible( false );
		SetSizeable( false );

		OkButton = new Button( vgui, this, "OKButton", "#MessageBox_OK" );
		OkButton.SetCommand( "Close" );
		OkButton.AddActionSignalTarget( this );
	}

	public void SetOKButtonText( string text )
	{
		OkButton.SetText( text );

		InvalidateLayout();
	}

	public void DoModal( Panel frameOver )
	{
		ShowWindow( frameOver );

		Vgui.Input.SetAppModalSurface( this );
	}

	private void ShowWindow( Panel frameOver )
	{
		_frameOver = frameOver;

		SetVisible( true );
		SetEnabled( true );
		MoveToFront();

		if ( OkButton.Visible )
			OkButton.RequestFocus();
		else
			RequestFocus();

		InvalidateLayout();
	}

	public override void Close()
	{
		base.Close();

		MarkForDeletion();
	}

	public override void ApplySchemeSettings()
	{
		base.ApplySchemeSettings();

		var (wide, tall) = _messageLabel.GetContentSize();

		_messageLabel.SetSize( wide, tall );

		wide += Scaled( 100 );
		tall += Scaled( 100 );

		SetSize( wide, tall );

		if ( _frameOver is { } frame )
			SetPos( ((frame.Wide - wide) / 2) + frame.X, ((frame.Tall - tall) / 2) + frame.Y );
		else
			SetPos( (Vgui.Surface.ScreenWide - wide) / 2, (Vgui.Surface.ScreenTall - tall) / 2 );
	}

	public override void PerformLayout()
	{
		var (x, y, wide, tall) = GetClientArea();

		wide += x;
		tall += y;

		var boxWide = Wide;
		var boxTall = Tall;

		ResizeButton( OkButton );

		boxWide = Math.Max( boxWide, _messageLabel.Wide + Scaled( 100 ) );
		boxWide = Math.Max( boxWide, (OkButton.Wide * 2) + 100 );

		SetSize( boxWide, boxTall );

		_messageLabel.SetPos( ((wide / 2) - (_messageLabel.Wide / 2)) + x, Scaled( 15 ) + y );
		OkButton.SetPos( ((wide / 2) - (OkButton.Wide / 2)) + x, tall - OkButton.Tall - Scaled( 15 ) );

		base.PerformLayout();
	}

	protected void ResizeButton( Button button )
	{
		var (contentWide, contentTall) = button.GetContentSize();

		button.SetSize( Math.Max( button.Wide, contentWide + Scaled( 10 ) ), Math.Max( button.Tall, contentTall + Scaled( 10 ) ) );
	}
}

public sealed class QueryBox : MessageBox
{
	private readonly Button _cancelButton;

	private Message _okCommand;
	private Message _cancelCommand;

	public QueryBox( Vgui vgui, string title, string text, Panel parent ) : base( vgui, title, text, parent )
	{
		SetHdProportional( true );

		_cancelButton = new Button( vgui, this, "CancelButton", "#QueryBox_Cancel" );
		_cancelButton.SetCommand( "Cancel" );

		OkButton.SetCommand( "OK" );
		OkButton.TabPosition = 1;

		_cancelButton.TabPosition = 2;
	}

	public void SetOKCommand( Message message ) => _okCommand = message;

	public void SetCancelCommand( Message message ) => _cancelCommand = message;

	public override void OnCommand( string command )
	{
		Message signal = null;
		var handled = false;

		if ( command.Equals( "OK", StringComparison.OrdinalIgnoreCase ) )
		{
			signal = _okCommand;
			handled = true;
		}
		else if ( command.Equals( "Cancel", StringComparison.OrdinalIgnoreCase ) )
		{
			signal = _cancelCommand;
			handled = true;
		}

		if ( handled )
		{
			OnCommand( "Close" );

			if ( signal is not null )
				PostActionSignal( new Message( signal.Name ) { Text = signal.Text } );

			MarkForDeletion();
		}

		base.OnCommand( command );
	}

	public override void OnKeyCodeTyped( KeyCode code )
	{
		if ( code == KeyCode.Escape )
			OnCommand( "Cancel" );
		else
			base.OnKeyCodeTyped( code );
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		var (x, y, wide, tall) = GetClientArea();

		wide += x;
		tall += y;

		ResizeButton( _cancelButton );

		OkButton.SetPos( ((wide / 2) + x) - OkButton.Wide - Scaled( 1 ), tall - OkButton.Tall - Scaled( 15 ) );
		_cancelButton.SetPos( (wide / 2) + Scaled( 16 ) + x, tall - _cancelButton.Tall - Scaled( 15 ) );
	}
}
