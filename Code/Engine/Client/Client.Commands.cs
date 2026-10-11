using System;
using System.Text;

namespace GoldSrc;

partial class Client
{
	private void EntitiesCommand()
	{
		var text = new StringBuilder();
		var arguments = new byte[56];

		for ( var i = 0; i < _entities.Count; i++ )
		{
			text.Append( $"{i,3}:" );

			if ( _entities[i] is not { } entity || (entity.Model ?? GetModelByIndex( entity.ModelIndex )) is not { } model )
				continue;

			BitConverter.TryWriteBytes( arguments.AsSpan( 0 ), (double)entity.CurState.Frame );
			BitConverter.TryWriteBytes( arguments.AsSpan( 8 ), (double)entity.Origin.x );
			BitConverter.TryWriteBytes( arguments.AsSpan( 16 ), (double)entity.Origin.y );
			BitConverter.TryWriteBytes( arguments.AsSpan( 24 ), (double)entity.Origin.z );
			BitConverter.TryWriteBytes( arguments.AsSpan( 32 ), (double)entity.Angles.x );
			BitConverter.TryWriteBytes( arguments.AsSpan( 40 ), (double)entity.Angles.y );
			BitConverter.TryWriteBytes( arguments.AsSpan( 48 ), (double)entity.Angles.z );

			text.Append( $"{model.Name}:{BitConverter.ToInt32( arguments, 0 ),2}  " );
			text.Append( $"({BitConverter.ToDouble( arguments, 4 ),5:F1},{BitConverter.ToDouble( arguments, 12 ),5:F1},{BitConverter.ToDouble( arguments, 20 ),5:F1}) " );
			text.Append( $"[{BitConverter.ToDouble( arguments, 28 ),5:F1} {BitConverter.ToDouble( arguments, 36 ),5:F1} {BitConverter.ToDouble( arguments, 44 ),5:F1}]\n" );

			Con.Printf( text.ToString() );

			text.Clear();
		}

		if ( text.Length != 0 )
			Con.Printf( text.ToString() );
	}

	private void SpecPosCommand( string[] arguments )
	{
		var origin = _viewOrigin;
		var pitch = _viewAngles.x;
		var yaw = _viewAngles.y;

		if ( arguments.Length == 1 && Common.Atoi( arguments[0] ) == 2 && _state == ClientState.Active )
		{
			ref readonly var state = ref GetFrame( _parseCountMod ).PlayerState[_playerNum];

			origin = state.Origin;
			pitch = state.Angles.x;
			yaw = state.Angles.y;
		}

		Con.Printf( $"spec_pos {origin.x:F1} {origin.y:F1} {origin.z:F1} {pitch:F1} {yaw:F1}\n" );
	}

	[ConCmd( "entities" )]
	private static void EntitiesConsoleCommand() => Current?.EntitiesCommand();

	[ConCmd( "spec_pos" )]
	private static void SpecPosConsoleCommand( params string[] arguments ) => Current?.SpecPosCommand( arguments );
}
