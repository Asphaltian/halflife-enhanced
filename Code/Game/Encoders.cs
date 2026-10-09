using GoldSrc;

namespace HalfLife;

public static class Encoders
{
	private const int FieldOrigin0 = 0;
	private const int FieldOrigin1 = 1;
	private const int FieldOrigin2 = 2;
	private const int FieldAngles0 = 3;
	private const int FieldAngles1 = 4;
	private const int FieldAngles2 = 5;

	private const int CustomFieldSkin = 6;
	private const int CustomFieldSequence = 7;
	private const int CustomFieldAnimTime = 8;

	private const int BeamPoints = 0;
	private const int BeamEntPoint = 1;
	private const int BeamEnts = 2;

	private static string[] EntityFieldNames = ["origin[0]", "origin[1]", "origin[2]", "angles[0]", "angles[1]", "angles[2]"];
	private static string[] PlayerFieldNames = ["origin[0]", "origin[1]", "origin[2]"];
	private static string[] CustomFieldNames = ["origin[0]", "origin[1]", "origin[2]", "angles[0]", "angles[1]", "angles[2]", "skin", "sequence", "animtime"];

	private static int[] EntityFields = new int[6];
	private static int[] PlayerFields = new int[3];
	private static int[] CustomFields = new int[9];

	private static bool _entityInitialized;
	private static bool _playerInitialized;
	private static bool _customInitialized;

	private static void FieldInit( Delta fields, string[] names, int[] indices )
	{
		for ( var i = 0; i < names.Length; i++ )
			indices[i] = fields.FindFieldIndex( names[i] );
	}

	public sealed class Entity : IDeltaEncoder
	{
		public void Encode( Delta fields, byte[] from, byte[] to )
		{
			if ( !_entityInitialized )
			{
				FieldInit( fields, EntityFieldNames, EntityFields );
				_entityInitialized = true;
			}

			if ( StructImage.ReadInt( to, EntityState.NumberOffset ) - 1 == Server.Current.CurrentPlayer )
			{
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin2] );
			}

			if ( StructImage.ReadFloat( to, EntityState.ImpactTimeOffset ) != 0f && StructImage.ReadFloat( to, EntityState.StartTimeOffset ) != 0f )
			{
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin2] );

				fields.UnsetFieldByIndex( EntityFields[FieldAngles0] );
				fields.UnsetFieldByIndex( EntityFields[FieldAngles1] );
				fields.UnsetFieldByIndex( EntityFields[FieldAngles2] );
			}

			var aimEnt = StructImage.ReadInt( to, EntityState.AimEntOffset );

			if ( StructImage.ReadInt( to, EntityState.MoveTypeOffset ) == (int)MoveType.Follow && aimEnt != 0 )
			{
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin2] );
			}
			else if ( aimEnt != StructImage.ReadInt( from, EntityState.AimEntOffset ) )
			{
				fields.SetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.SetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.SetFieldByIndex( EntityFields[FieldOrigin2] );
			}
		}
	}

	public sealed class Player : IDeltaEncoder
	{
		public void Encode( Delta fields, byte[] from, byte[] to )
		{
			if ( !_playerInitialized )
			{
				FieldInit( fields, PlayerFieldNames, PlayerFields );
				_playerInitialized = true;
			}

			if ( StructImage.ReadInt( to, EntityState.NumberOffset ) - 1 == Server.Current.CurrentPlayer )
			{
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin2] );
			}

			var aimEnt = StructImage.ReadInt( to, EntityState.AimEntOffset );

			if ( StructImage.ReadInt( to, EntityState.MoveTypeOffset ) == (int)MoveType.Follow && aimEnt != 0 )
			{
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.UnsetFieldByIndex( EntityFields[FieldOrigin2] );
			}
			else if ( aimEnt != StructImage.ReadInt( from, EntityState.AimEntOffset ) )
			{
				fields.SetFieldByIndex( EntityFields[FieldOrigin0] );
				fields.SetFieldByIndex( EntityFields[FieldOrigin1] );
				fields.SetFieldByIndex( EntityFields[FieldOrigin2] );
			}
		}
	}

	public sealed class Custom : IDeltaEncoder
	{
		public void Encode( Delta fields, byte[] from, byte[] to )
		{
			if ( !_customInitialized )
			{
				FieldInit( fields, CustomFieldNames, CustomFields );
				_customInitialized = true;
			}

			var beamType = StructImage.ReadInt( to, EntityState.RenderModeOffset ) & 0x0F;

			if ( beamType != BeamPoints && beamType != BeamEntPoint )
			{
				fields.UnsetFieldByIndex( CustomFields[FieldOrigin0] );
				fields.UnsetFieldByIndex( CustomFields[FieldOrigin1] );
				fields.UnsetFieldByIndex( CustomFields[FieldOrigin2] );
			}

			if ( beamType != BeamPoints )
			{
				fields.UnsetFieldByIndex( CustomFields[FieldAngles0] );
				fields.UnsetFieldByIndex( CustomFields[FieldAngles1] );
				fields.UnsetFieldByIndex( CustomFields[FieldAngles2] );
			}

			if ( beamType != BeamEnts && beamType != BeamEntPoint )
			{
				fields.UnsetFieldByIndex( CustomFields[CustomFieldSkin] );
				fields.UnsetFieldByIndex( CustomFields[CustomFieldSequence] );
			}

			if ( (int)StructImage.ReadFloat( from, EntityState.AnimTimeOffset ) == (int)StructImage.ReadFloat( to, EntityState.AnimTimeOffset ) )
				fields.UnsetFieldByIndex( CustomFields[CustomFieldAnimTime] );
		}
	}

	public static void Register()
	{
		Delta.AddEncoder( "Entity_Encode", new Entity() );
		Delta.AddEncoder( "Custom_Encode", new Custom() );
		Delta.AddEncoder( "Player_Encode", new Player() );
	}
}
