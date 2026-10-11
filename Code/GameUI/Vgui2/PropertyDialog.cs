using System;

namespace GoldSrc.GameUI;

public class PropertyDialog : Frame
{
	private readonly PropertySheet _propertySheet;
	private readonly Button _okButton;
	private readonly Button _cancelButton;
	private readonly Button _applyButton;

	public PropertyDialog( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		_propertySheet = new PropertySheet( vgui, this, "Sheet" );
		_propertySheet.AddActionSignalTarget( this );
		_propertySheet.TabPosition = 1;

		_okButton = new Button( vgui, this, "OKButton", "#PropertyDialog_OK" );
		_okButton.AddActionSignalTarget( this );
		_okButton.TabPosition = 2;
		_okButton.SetCommand( "OK" );

		NavGroup.SetDefaultButton( _okButton );

		_cancelButton = new Button( vgui, this, "CancelButton", "#PropertyDialog_Cancel" );
		_cancelButton.AddActionSignalTarget( this );
		_cancelButton.TabPosition = 3;
		_cancelButton.SetCommand( "Close" );

		_applyButton = new Button( vgui, this, "ApplyButton", "#PropertyDialog_Apply" );
		_applyButton.AddActionSignalTarget( this );
		_applyButton.TabPosition = 4;
		_applyButton.SetVisible( false );
		_applyButton.SetEnabled( false );
		_applyButton.SetCommand( "Apply" );

		SetSizeable( false );
	}

	public PropertySheet PropertySheet => _propertySheet;

	public void AddPage( PropertyPage page, string title ) => _propertySheet.AddPage( page, title );

	public void ResetAllData() => _propertySheet.ResetAllData();

	public void SetOKButtonText( string text ) => _okButton.SetText( text );

	public void SetApplyButtonVisible( bool state )
	{
		_applyButton.SetVisible( state );

		InvalidateLayout();
	}

	public void EnableApplyButton( bool state )
	{
		_applyButton.SetEnabled( state );

		InvalidateLayout();
	}

	protected virtual void OnOK()
	{
		_propertySheet.ApplyChanges();

		PostActionSignal( new Message( "ApplyChanges" ) );
	}

	public override void RequestFocus( int direction = 0 ) => _propertySheet.RequestFocus( direction );

	public override void OnMessage( Message message, Panel from )
	{
		if ( message.Name == "ApplyButtonEnable" )
		{
			if ( !_applyButton.Enabled )
				EnableApplyButton( true );

			return;
		}

		base.OnMessage( message, from );
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "OK", StringComparison.OrdinalIgnoreCase ) )
		{
			OnOK();
			OnCommand( "Close" );
		}
		else if ( command.Equals( "Cancel", StringComparison.OrdinalIgnoreCase ) )
		{
			OnCommand( "Close" );
		}
		else if ( command.Equals( "Apply", StringComparison.OrdinalIgnoreCase ) )
		{
			OnOK();
			EnableApplyButton( false );
		}
		else
		{
			base.OnCommand( command );
		}
	}

	public override void PerformLayout()
	{
		base.PerformLayout();

		var (x, y, wide, tall) = GetClientArea();

		_propertySheet.SetBounds( x, y, wide, tall - Scaled( 32 ) );

		var buttonX = (x + wide) - Scaled( 80 );
		var buttonY = (y + tall) - Scaled( 28 );

		if ( _applyButton.Visible )
		{
			_applyButton.SetBounds( buttonX, buttonY, Scaled( 72 ), Scaled( 24 ) );

			buttonX -= Scaled( 80 );
		}

		if ( _cancelButton.Visible )
		{
			_cancelButton.SetBounds( buttonX, buttonY, Scaled( 72 ), Scaled( 24 ) );

			buttonX -= Scaled( 80 );
		}

		_okButton.SetBounds( buttonX, buttonY, Scaled( 72 ), Scaled( 24 ) );

		_propertySheet.InvalidateLayout();
	}
}
