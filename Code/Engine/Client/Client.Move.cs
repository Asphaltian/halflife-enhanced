namespace GoldSrc;

partial class Client
{
	[ConVar( "cl_solid_players" )]
	public static float SolidPlayers { get; set; } = 1f;

	private static readonly string[] PlayerPhysEntNames = CreatePlayerPhysEntNames();

	private readonly PlayerMove _pmove = new();
	private readonly PhysEntPool _physEntPool = new();

	private bool _pushed;
	private int _pushedPhysEnts;
	private int _pushedVisEnts;

	public Vector3 LocalPlayerViewHeight => _viewHeight;

	public bool IsThirdPerson => _clientDll.IsThirdPerson();

	public int WindowCenterX => (int)Screen.Width / 2;

	public int WindowCenterY => (int)Screen.Height / 2;

	private static string[] CreatePlayerPhysEntNames()
	{
		var names = new string[Server.MaxClientsLimit];

		for ( var i = 0; i < names.Length; i++ )
			names[i] = $"player {i}";

		return names;
	}

	public PmTrace TraceGlow( Vector3 start, Vector3 end )
	{
		_pmove.UseHull = 2;

		return _pmove.PlayerTrace( start, end, TraceFlags.StudioIgnore | TraceFlags.GlassIgnore, -1 );
	}

	public ClientEntity GetLocalPlayer() => GetEntity( _playerNum + 1 );

	public void GetMousePosition( out int x, out int y )
	{
		x = (int)Mouse.Position.x;
		y = (int)Mouse.Position.y;
	}

	public int PointContents( Vector3 point ) => _pmove.PointContents( point );

	public float? WaterEntityScale( Vector3 point )
	{
		_pmove.WaterEntity( point, out var info );

		return info >= 0 && GetEntity( info ) is { } water && GetBrushModel( water.CurState.ModelIndex ) is not null ? water.CurState.Scale : null;
	}

	public void PushPMStates()
	{
		if ( _pushed )
		{
			Con.Printf( "CL_PushPMStates called with pushed stack\n" );
			return;
		}

		_pushedPhysEnts = _pmove.PhysEnts.Count;
		_pushedVisEnts = _pmove.VisEnts.Count;
		_pushed = true;
	}

	public void PopPMStates()
	{
		if ( !_pushed )
		{
			Con.Printf( "CL_PopPMStates called without stack\n" );
			return;
		}

		_pushed = false;
		_pmove.PhysEnts.RemoveRange( _pushedPhysEnts, _pmove.PhysEnts.Count - _pushedPhysEnts );
		_pmove.VisEnts.RemoveRange( _pushedVisEnts, _pmove.VisEnts.Count - _pushedVisEnts );
	}

	public void SetSolidPlayers( int playerNumber )
	{
		if ( SolidPlayers == 0f )
			return;

		var frame = GetFrame( _parseCountMod );

		for ( var i = 0; i < _predictedPlayers.Length; i++ )
		{
			ref readonly var player = ref _predictedPlayers[i];

			if ( playerNumber == -1 )
			{
				if ( i != _playerNum && !player.Active )
					continue;
			}
			else if ( !player.Active || i == playerNumber )
			{
				continue;
			}

			if ( player.Solid == 0 )
				continue;

			ref readonly var state = ref frame.PlayerState[i];

			var physEnt = _physEntPool.Alloc();

			physEnt.Name = PlayerPhysEntNames[i];
			physEnt.Player = 1;
			physEnt.Origin = player.Origin;
			physEnt.Mins = PlayerMove.PlayerMins[player.UseHull];
			physEnt.Maxs = PlayerMove.PlayerMaxs[player.UseHull];
			physEnt.Info = i + 1;
			physEnt.Angles = player.Angles;
			physEnt.Solid = (Solid)player.Solid;
			physEnt.Skin = 0;
			physEnt.RenderMode = (RenderMode)state.RenderMode;
			physEnt.Frame = state.Frame;
			physEnt.Sequence = state.Sequence;
			physEnt.MoveType = (MoveType)player.MoveType;
			physEnt.TakeDamage = 1;
			physEnt.ClassNumber = state.PlayerClass;

			physEnt.Controller[0] = state.Controller0;
			physEnt.Controller[1] = state.Controller1;
			physEnt.Controller[2] = state.Controller2;
			physEnt.Controller[3] = state.Controller3;
			physEnt.Blending[0] = state.Blending0;
			physEnt.Blending[1] = state.Blending1;

			_pmove.PhysEnts.Add( physEnt );
		}
	}

	public void SetTraceHull( int hull ) => _pmove.UseHull = hull;

	public PmTrace PlayerTrace( Vector3 start, Vector3 end, TraceFlags traceFlags, int ignore )
	{
		return _pmove.PlayerTrace( start, end, traceFlags, ignore );
	}

	public PmTrace TraceLine( Vector3 start, Vector3 end, int flags, int useHull, int ignore )
	{
		return _pmove.TraceLine( start, end, flags, useHull, ignore );
	}

	public int IndexFromTrace( in PmTrace trace )
	{
		return trace.Ent >= 0 && trace.Ent < _pmove.PhysEnts.Count ? _pmove.PhysEnts[trace.Ent].Info : -1;
	}

	public PhysEnt GetPhysEnt( int index )
	{
		return index >= 0 && index < _pmove.PhysEnts.Count ? _pmove.PhysEnts[index] : null;
	}

	public string TraceTexture( int ground, Vector3 start, Vector3 end )
	{
		if ( ground < 0 || ground >= _pmove.PhysEnts.Count || _pmove.PhysEnts[ground].Model is not { } model )
			return null;

		var firstNode = 0;

		if ( ground >= 1 )
		{
			var physEnt = _pmove.PhysEnts[ground];
			var offset = model.Hulls[0].ClipMins + physEnt.Origin;

			start -= offset;
			end -= offset;
			firstNode = model.Hulls[0].FirstClipNode;

			if ( physEnt.Angles.x != 0f || physEnt.Angles.y != 0f || physEnt.Angles.z != 0f )
			{
				MathLib.AngleVectors( physEnt.Angles, out var forward, out var right, out var up );

				start = new Vector3( Vector3.Dot( start, forward ), -Vector3.Dot( start, right ), Vector3.Dot( start, up ) );
				end = new Vector3( Vector3.Dot( end, forward ), -Vector3.Dot( end, right ), Vector3.Dot( end, up ) );
			}
		}

		var file = model.File;

		if ( file.Nodes.Length == 0 )
			return null;

		var face = file.SurfaceAtPoint( firstNode, start, end );

		return face < 0 ? null : file.GetTextureName( file.Faces[face] );
	}

	private void SetSolidEntities()
	{
		var world = WorldModel;

		_pmove.Scene = Scene;
		_pmove.PhysEnts.Clear();
		_pmove.VisEnts.Clear();
		_pmove.MoveEnts.Clear();

		_physEntPool.Reset();

		var worldEnt = _physEntPool.Alloc();

		worldEnt.Name = world?.Name;
		worldEnt.Model = world;
		worldEnt.Solid = Solid.Bsp;
		worldEnt.MoveType = MoveType.None;

		_pmove.PhysEnts.Add( worldEnt );
		_pmove.VisEnts.Add( _pmove.PhysEnts[0] );

		var pack = CurrentFrame.Entities;

		for ( var i = 0; i < pack.Count; i++ )
		{
			ref readonly var state = ref pack.Entities[i];

			if ( state.Number >= 1 && state.Number <= _maxClients )
				continue;

			if ( state.ModelIndex == 0 )
				continue;

			var brush = GetBrushModel( state.ModelIndex );
			var studio = GetStudioModel( state.ModelIndex );

			if ( brush is null && studio is null )
				continue;

			if ( state.Owner != 0 && state.Owner - 1 == _playerNum )
				continue;

			var physEnt = CopyEntityToPhysEnt( state, brush, studio, GetModelByIndex( state.ModelIndex ).Name );

			_pmove.VisEnts.Add( physEnt );

			if ( state.Solid == (int)Solid.Trigger )
				continue;

			if ( state.Solid == (int)Solid.Not && state.Skin >= -1 )
				continue;

			if ( state.Mins.z == 0f && state.Maxs.z == 1f )
				continue;

			if ( MathLib.Length( state.Maxs - state.Mins ) == 0f )
				continue;

			if ( state.Solid == (int)Solid.Not && state.Skin == -16 )
			{
				if ( _pmove.MoveEnts.Count < PlayerMove.MaxMoveEnts )
					_pmove.MoveEnts.Add( physEnt );
				else
					Con.DPrintf( "CL_SetSolidEntities:  pmove->nummoveent >= MAX_MOVEENTS\n" );
			}
			else if ( _pmove.PhysEnts.Count < PlayerMove.MaxPhysEnts )
			{
				_pmove.PhysEnts.Add( physEnt );
			}
			else
			{
				Con.DPrintf( "CL_SetSolidEntities:  pmove->numphysent >= MAX_PHYSENTS\n" );
			}
		}
	}

	private PhysEnt CopyEntityToPhysEnt( in EntityState entity, BrushModel brush, Studio.File studio, string modelName )
	{
		var physEnt = _physEntPool.Alloc();

		physEnt.Info = entity.Number;
		physEnt.Origin = entity.Origin;
		physEnt.Angles = entity.Angles;
		physEnt.Mins = entity.Mins;
		physEnt.Maxs = entity.Maxs;
		physEnt.Solid = (Solid)entity.Solid;
		physEnt.Skin = entity.Skin;
		physEnt.RenderMode = (RenderMode)entity.RenderMode;
		physEnt.Frame = entity.Frame;
		physEnt.Sequence = entity.Sequence;
		physEnt.MoveType = (MoveType)entity.MoveType;
		physEnt.ClassNumber = entity.PlayerClass;

		if ( entity.Solid == (int)Solid.Bsp )
		{
			physEnt.Model = brush;
			physEnt.Name = brush?.Name;
		}
		else if ( entity.Solid == (int)Solid.BBox )
		{
			physEnt.Name = modelName;

			if ( studio is not null && (studio.Header.Flags & StudioHulls.TraceHitBox) != 0 )
				physEnt.StudioModel = studio;
		}
		else if ( studio is null )
		{
			physEnt.Model = brush;
			physEnt.Name = brush?.Name;
		}
		else
		{
			physEnt.StudioModel = studio;
			physEnt.Name = modelName;
		}

		physEnt.Controller[0] = entity.Controller0;
		physEnt.Controller[1] = entity.Controller1;
		physEnt.Controller[2] = entity.Controller2;
		physEnt.Controller[3] = entity.Controller3;
		physEnt.Blending[0] = entity.Blending0;
		physEnt.Blending[1] = entity.Blending1;

		return physEnt;
	}
}
