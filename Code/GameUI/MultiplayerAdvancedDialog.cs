using System;
using System.Text;

namespace GoldSrc.GameUI;

public sealed class MultiplayerControl : Panel
{
	public ScriptObjectType Type;
	public Panel Control;
	public Label Prompt;
	public ScriptObject ScriptObject;

	public MultiplayerControl( Vgui vgui, Panel parent, string name ) : base( vgui, parent, name )
	{
		SetPaintBackgroundEnabled( false );
	}

	public override void OnSizeChanged( int wide, int tall )
	{
		if ( Prompt is not null )
		{
			var half = wide / 2;

			Control?.SetBounds( half + 20, 4, half - 20, tall - 8 );
			Prompt.SetBounds( 0, 4, half + 20, tall - 8 );

			return;
		}

		Control?.SetBounds( 0, 4, wide, tall - 8 );
	}
}

public sealed class MultiplayerAdvancedDialog : Frame
{
	private const string ScriptFileName = "user.scr";

	private readonly InfoDescription _description;
	private readonly PanelListPanel _listPanel;
	private readonly List<MultiplayerControl> _list = [];

	public MultiplayerAdvancedDialog( Vgui vgui, Panel parent ) : base( vgui, parent, "MultiplayerAdvancedDialog" )
	{
		SetBounds( 0, 0, Scaled( 372 ), Scaled( 160 ) );
		SetSizeable( false );
		SetCloseSound( "sound/UI/launch_dnmenu1.wav" );

		BasePanel.PositionDialog( this );

		SetTitle( "#GameUI_MultiplayerAdvanced" );

		new Button( vgui, this, "Cancel", "#GameUI_Cancel" ).SetCommand( "Close" );
		new Button( vgui, this, "OK", "#GameUI_OK" ).SetCommand( "Ok" );

		_listPanel = new PanelListPanel( vgui, this, "PanelListPanel" );

		_description = new InfoDescription();
		_description.InitFromFile( ScriptFileName );
		_description.TransferCurrentValues();

		CreateControls();

		LoadControlSettings( "Resource\\MultiplayerAdvancedDialog.res" );
	}

	private void DestroyControls()
	{
		foreach ( var control in _list )
		{
			control.Control?.MarkForDeletion();
			control.Prompt?.MarkForDeletion();
			control.MarkForDeletion();
		}

		_list.Clear();
	}

	private void CreateControls()
	{
		DestroyControls();

		foreach ( var scriptObject in _description.Objects )
		{
			var control = new MultiplayerControl( Vgui, _listPanel, "mpcontrol_t" ) { Type = scriptObject.Type };

			switch ( control.Type )
			{
				case ScriptObjectType.Bool:
					{
						var box = new CheckButton( Vgui, control, "DescCheckButton", scriptObject.Prompt );

						box.SetSelected( scriptObject.DefValueFloat != 0f );

						control.Control = box;
						break;
					}

				case ScriptObjectType.Number:
				case ScriptObjectType.String:
					{
						var edit = new TextEntry( Vgui, control, "DescTextEntry" );

						edit.InsertString( scriptObject.DefValue );

						control.Control = edit;
						break;
					}

				case ScriptObjectType.List:
					{
						var combo = new ComboBox( Vgui, control, "DescComboBox", 5, false );

						foreach ( var item in scriptObject.ListItems )
							combo.AddItem( item.ItemText );

						combo.ActivateItemByRow( (int)scriptObject.DefValueFloat );

						control.Control = combo;
						break;
					}
			}

			if ( control.Type != ScriptObjectType.Bool )
			{
				control.Prompt = new Label( Vgui, control, "DescLabel", "" );
				control.Prompt.SetContentAlignment( Alignment.West );
				control.Prompt.SetTextInset( Scaled( 5 ), 0 );
				control.Prompt.SetText( scriptObject.Prompt );
			}

			control.ScriptObject = scriptObject;
			control.SetSize( Scaled( 100 ), Scaled( 28 ) );

			_listPanel.AddItem( control );
			_list.Add( control );
		}
	}

	private void GatherCurrentValues()
	{
		foreach ( var control in _list )
		{
			var scriptObject = control.ScriptObject;

			if ( control.Control is null )
			{
				scriptObject.SetCurValue( scriptObject.DefValue );
				continue;
			}

			string value;

			switch ( scriptObject.Type )
			{
				case ScriptObjectType.Bool:
					value = ((CheckButton)control.Control).IsSelected ? "1" : "0";
					break;

				case ScriptObjectType.Number:
				case ScriptObjectType.String:
					value = ((TextEntry)control.Control).Text;
					break;

				case ScriptObjectType.List:
					{
						var index = ((ComboBox)control.Control).GetActiveItem();

						value = index < scriptObject.ListItems.Count ? scriptObject.ListItems[Math.Max( index, 0 )].Value : scriptObject.DefValue;
						break;
					}

				default:
					value = scriptObject.DefValue;
					break;
			}

			scriptObject.SetCurValue( ScriptObject.StripInvalidCharacters( value ) );
		}
	}

	private void SaveValues()
	{
		GatherCurrentValues();

		_description.WriteToConfig();

		var file = new StringBuilder();

		_description.WriteToScriptFile( file );

		Server.WriteFile( ScriptFileName, Encoding.Latin1.GetBytes( file.ToString() ) );
	}

	public override void OnCommand( string command )
	{
		if ( command.Equals( "Ok", StringComparison.OrdinalIgnoreCase ) )
		{
			SaveValues();
			Close();

			return;
		}

		base.OnCommand( command );
	}
}
