using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UtilLibs;

//Licensed from Sgt_imalas

namespace Rephysicalized
{
	class WorldElementDropper : KMonoBehaviour
	{
		[MyCmpGet] Rotatable rotatable;

		public Storage TargetStorage;
		[SerializeField]
		public bool DropSolids = false;
		[SerializeField]
		public bool DropLiquids = false;
		[SerializeField]
		public bool DropGases = false;

		[SerializeField]
		public CellOffset SpawnOffset = CellOffset.none;

		private int storageChangeHandle = -1;

		private static readonly System.Action<object, object> OnStorageChangedDelegate =
			(System.Action<object, object>)((component, data) => ((WorldElementDropper)component).OnStorageChanged(data));

		public override void OnCleanUp()
		{
			base.OnCleanUp();
			this.Unsubscribe(ref storageChangeHandle);

		}
		public override void OnSpawn()
		{
			base.OnSpawn();
			storageChangeHandle = this.Subscribe((int)GameHashes.OnStorageChange, OnStorageChangedDelegate, this);
		}
		void OnStorageChanged(object data)
		{
			var tagsInStorage = TargetStorage.GetAllIDsInStorage();
			HashSet<Tag> toDrop = new();
			var rotatedOffset = rotatable == null ? SpawnOffset : rotatable.GetRotatedCellOffset(SpawnOffset);
			var offset = rotatedOffset.ToVector3();

			foreach (var tag in tagsInStorage)
			{
				var element = ElementLoader.GetElement(tag);
				if (element == null)
					continue;
				if (element.IsSolid && DropSolids)
					toDrop.Add(tag);
				else if (element.IsLiquid && DropLiquids)
					toDrop.Add(tag);
				else if (element.IsGas && DropGases)
					toDrop.Add(tag);
			}
			foreach (var dropIt in toDrop)
			{
				TargetStorage.DropSome(dropIt, 9999999999, true, true, offset);
			}
		}
	}
}
