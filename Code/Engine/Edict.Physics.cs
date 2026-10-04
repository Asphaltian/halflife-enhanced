namespace GoldSrc;

partial class Edict
{
	private ModelCollider _brushCollider;
	private BoxCollider _boxCollider;
	private int _brushColliderModel;

	protected override void OnPreRender()
	{
		var server = Server;

		if ( server.Active && !ClientSide )
			UpdateCollision( server );
	}

	private void UpdateCollision( Server server )
	{
		if ( Index != 0 && !Tags.Has( ScenePhysics.ShadowTag ) )
			Tags.Add( ScenePhysics.ShadowTag );

		var brush = Solid == Solid.Bsp && server.GetModel( ModelIndex ) is not null;

		if ( brush && _brushCollider is null )
		{
			_brushCollider = AddComponent<ModelCollider>();
			_brushCollider.Static = Index == 0;
		}

		if ( _brushCollider is not null )
		{
			if ( brush && _brushColliderModel != ModelIndex )
			{
				_brushColliderModel = ModelIndex;
				_brushCollider.Model = server.GetRenderModel( ModelIndex );
			}

			_brushCollider.Enabled = brush;
		}

		var box = !brush && (Index < 1 || Index > server.MaxClients) && Solid is Solid.BBox or Solid.SlideBox && Size != Vector3.Zero;

		if ( box && _boxCollider is null )
			_boxCollider = new GameObject( GameObject, true, "bbox" ).AddComponent<BoxCollider>();

		if ( _boxCollider is null )
			return;

		_boxCollider.Enabled = box;

		if ( !box )
			return;

		_boxCollider.WorldRotation = Rotation.Identity;
		_boxCollider.Center = (Mins + Maxs) * 0.5f;
		_boxCollider.Scale = Maxs - Mins;
	}
}
