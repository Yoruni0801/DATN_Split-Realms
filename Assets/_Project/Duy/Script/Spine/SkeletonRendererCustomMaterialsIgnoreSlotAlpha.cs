/******************************************************************************
 * Derived from Spine.Unity.SkeletonRendererCustomMaterials.
 *****************************************************************************/

#if UNITY_2018_3 || UNITY_2019 || UNITY_2018_3_OR_NEWER
#define NEW_PREFAB_SYSTEM
#endif

using System.Collections.Generic;
using UnityEngine;

namespace Spine.Unity
{
#if NEW_PREFAB_SYSTEM
	[ExecuteAlways]
#else
	[ExecuteInEditMode]
#endif
	[AddComponentMenu(
		"Spine/SkeletonRenderer Custom Materials Ignore Slot Alpha"
	)]
	public class SkeletonRendererCustomMaterialsIgnoreSlotAlpha
		: SkeletonRendererCustomMaterials
	{
		[System.Serializable]
		public class ElementSlotMaterialOverride
		{
			[HideInInspector]
			public string skillName;

			public SkillElementType elementType = SkillElementType.Fire;

			[SpineSlot]
			public string slotName;

			public Material material;

			[Tooltip("Tắt override này mà không cần xóa khỏi danh sách.")]
			public bool overrideDisabled;
		}

		private static readonly int MainTexId =
			Shader.PropertyToID("_MainTex");

		[Header("Slot Settings")]
		[SerializeField]
		private bool ignoreSlotAlpha = true;

		[SerializeField]
		private bool copyAttachmentMainTexture = true;

		[Header("Active Skill Slot Materials")]
		[SerializeField]
		private bool followPlayerSkill = true;

		[SerializeField]
		private List<ElementSlotMaterialOverride> elementSlotMaterials =
			new List<ElementSlotMaterialOverride>();

		/// <summary>
		/// Alpha gốc của các slot đang bị ép thành 1.
		/// </summary>
		private readonly Dictionary<Slot, float> originalSlotAlpha =
			new Dictionary<Slot, float>();

		/// <summary>
		/// Các slot dùng customSlotMaterials thông thường.
		/// </summary>
		private readonly List<Slot> activeSlots =
			new List<Slot>();

		/// <summary>
		/// Material nguyên tố hiện đang được áp dụng theo Slot.
		/// </summary>
		private readonly Dictionary<Slot, Material>
			activeElementSlotMaterials =
				new Dictionary<Slot, Material>();

		private Player elementPlayer;
		private string currentSkinName;
		private SkillElementType currentSkillElement;
		private bool hasActiveSkill;

#if UNITY_EDITOR
		private void Reset()
		{
			skeletonRenderer = GetComponent<SkeletonRenderer>();
			EnsureElementSlotMaterialsList();
		}

		private void OnValidate()
		{
			EnsureElementSlotMaterialsList();
		}

		private void EnsureElementSlotMaterialsList()
		{
			if (elementSlotMaterials == null)
			{
				elementSlotMaterials =
					new List<ElementSlotMaterialOverride>();
			}
		}
#endif

		private void OnEnable()
		{
			if (skeletonRenderer == null)
			{
				skeletonRenderer = GetComponent<SkeletonRenderer>();
			}

			if (skeletonRenderer == null)
			{
				Debug.LogError(
					"skeletonRenderer == null",
					this
				);

				return;
			}

			skeletonRenderer.Initialize(false);

			skeletonRenderer.OnRebuild -= HandleRebuild;
			skeletonRenderer.OnRebuild += HandleRebuild;

			BindPlayerSkill();

			SetCustomMaterialOverrides();
			SetCustomSlotMaterials();
			ApplyCurrentElementSlotMaterials();
		}

		private void Update()
		{
			RefreshActiveSlots();
		}

		private void LateUpdate()
		{
			RefreshActiveSlots();
		}

		private void OnDisable()
		{
			if (skeletonRenderer == null)
			{
				return;
			}

			skeletonRenderer.OnRebuild -= HandleRebuild;

			UnbindPlayerElement();

			RemoveElementSlotMaterials();
			RemoveCustomMaterialOverrides();
			RemoveCustomSlotMaterials();

			RestoreSlotAlpha();
		}

		private void HandleRebuild(
			SkeletonRenderer rebuiltRenderer
		)
		{
			RestoreSlotAlpha();

			activeSlots.Clear();
			activeElementSlotMaterials.Clear();

			SetCustomMaterialOverrides();
			SetCustomSlotMaterials();
			ApplyCurrentElementSlotMaterials();
		}

		#region Static Custom Slot Materials

		/// <summary>
		/// Áp dụng Material thông thường.
		///
		/// Slot nào có trong elementSlotMaterials sẽ không được xử lý ở đây,
		/// vì slot đó phải phụ thuộc vào hệ hiện tại của Player.
		/// </summary>
		private void SetCustomSlotMaterials()
		{
			if (skeletonRenderer == null
				|| skeletonRenderer.skeleton == null)
			{
				return;
			}

			activeSlots.Clear();

			for (int i = 0; i < customSlotMaterials.Count; i++)
			{
				SlotMaterialOverride slotMaterialOverride =
					customSlotMaterials[i];

				if (slotMaterialOverride.overrideDisabled
					|| string.IsNullOrEmpty(
						slotMaterialOverride.slotName
					)
					|| slotMaterialOverride.material == null)
				{
					continue;
				}

				/*
				 * Slot được quản lý theo hệ không được áp dụng
				 * Material mặc định từ customSlotMaterials.
				 */
				if (IsElementManagedSlotName(
					slotMaterialOverride.slotName
				))
				{
					continue;
				}

				Slot slot =
					skeletonRenderer.skeleton.FindSlot(
						slotMaterialOverride.slotName
					);

				if (slot == null)
				{
					continue;
				}

				skeletonRenderer.CustomSlotMaterials[slot] =
					slotMaterialOverride.material;

				if (!activeSlots.Contains(slot))
				{
					activeSlots.Add(slot);
				}

				if (copyAttachmentMainTexture)
				{
					CopyAttachmentMainTexture(
						slot,
						slotMaterialOverride.material
					);
				}

				ForceSlotAlpha(slot);
			}
		}

		private void RemoveCustomSlotMaterials()
		{
			if (skeletonRenderer == null
				|| skeletonRenderer.skeleton == null)
			{
				activeSlots.Clear();
				return;
			}

			for (int i = 0; i < customSlotMaterials.Count; i++)
			{
				SlotMaterialOverride slotMaterialOverride =
					customSlotMaterials[i];

				if (string.IsNullOrEmpty(
					slotMaterialOverride.slotName
				))
				{
					continue;
				}

				/*
				 * Slot nguyên tố được xóa bởi
				 * RemoveElementSlotMaterials.
				 */
				if (IsElementManagedSlotName(
					slotMaterialOverride.slotName
				))
				{
					continue;
				}

				Slot slot =
					skeletonRenderer.skeleton.FindSlot(
						slotMaterialOverride.slotName
					);

				if (slot == null)
				{
					continue;
				}

				if (!skeletonRenderer.CustomSlotMaterials.TryGetValue(
					slot,
					out Material currentMaterial
				))
				{
					continue;
				}

				if (currentMaterial
					!= slotMaterialOverride.material)
				{
					continue;
				}

				skeletonRenderer.CustomSlotMaterials.Remove(slot);
			}

			activeSlots.Clear();
		}

		private void RefreshActiveSlots()
		{
			if (activeSlots.Count == 0)
			{
				RefreshSlotMaterialTextures();
			}

			for (int i = 0; i < activeSlots.Count; i++)
			{
				Slot slot = activeSlots[i];

				if (slot == null)
				{
					continue;
				}

				ForceSlotAlpha(slot);

				if (copyAttachmentMainTexture)
				{
					CopyAttachmentMainTexture(slot);
				}
			}

			RefreshElementSlots();
		}

		private void RefreshSlotMaterialTextures()
		{
			if (!copyAttachmentMainTexture
				|| skeletonRenderer == null
				|| skeletonRenderer.skeleton == null)
			{
				return;
			}

			for (int i = 0; i < customSlotMaterials.Count; i++)
			{
				SlotMaterialOverride slotMaterialOverride =
					customSlotMaterials[i];

				if (slotMaterialOverride.overrideDisabled
					|| string.IsNullOrEmpty(
						slotMaterialOverride.slotName
					)
					|| slotMaterialOverride.material == null)
				{
					continue;
				}

				if (IsElementManagedSlotName(
					slotMaterialOverride.slotName
				))
				{
					continue;
				}

				Slot slot =
					skeletonRenderer.skeleton.FindSlot(
						slotMaterialOverride.slotName
					);

				if (slot == null)
				{
					continue;
				}

				if (!activeSlots.Contains(slot))
				{
					activeSlots.Add(slot);
				}

				CopyAttachmentMainTexture(
					slot,
					slotMaterialOverride.material
				);
			}
		}

		#endregion

		#region Element Slot Materials

		private void BindPlayerSkill()
		{
			if (!followPlayerSkill
				|| !Application.isPlaying)
			{
				return;
			}

			elementPlayer = GetComponentInParent<Player>();

			if (elementPlayer == null)
			{
				currentSkinName = null;
				hasActiveSkill = false;
				return;
			}

			elementPlayer.OnSkillVisualStarted -= HandleSkillVisualStarted;
			elementPlayer.OnSkillVisualStarted += HandleSkillVisualStarted;
			elementPlayer.OnSkillVisualEnded -= HandleSkillVisualEnded;
			elementPlayer.OnSkillVisualEnded += HandleSkillVisualEnded;
		}

		private void UnbindPlayerElement()
		{
			if (elementPlayer != null)
			{
				elementPlayer.OnSkillVisualStarted -=
					HandleSkillVisualStarted;
				elementPlayer.OnSkillVisualEnded -=
					HandleSkillVisualEnded;
			}

			elementPlayer = null;
			currentSkinName = null;
			currentSkillElement = SkillElementType.Fire;
			hasActiveSkill = false;
		}

		private void HandleSkillVisualStarted(SkillData skill)
		{
			currentSkinName =
				skill != null ? skill.BowSkinName : null;
			currentSkillElement =
				skill != null ? skill.ElementType : SkillElementType.Fire;
			hasActiveSkill = skill != null;
			ApplyCurrentElementSlotMaterials();
		}

		public void BeginSkillMaterial(
			SkillElementType elementType,
			string skinName
		)
		{
			currentSkinName = skinName;
			currentSkillElement = elementType;
			hasActiveSkill = true;
			ApplyCurrentElementSlotMaterials();
		}

		private void HandleSkillVisualEnded(SkillData skill)
		{
			if (skill != null
				&& !string.IsNullOrEmpty(currentSkinName)
				&& skill.BowSkinName != currentSkinName)
			{
				return;
			}

			currentSkinName = null;
			hasActiveSkill = false;
			RemoveElementSlotMaterials();
		}

		public void EndSkillMaterial()
		{
			currentSkinName = null;
			hasActiveSkill = false;
			RemoveElementSlotMaterials();
		}

		/// <summary>
		/// Xóa Material skill cũ và chỉ áp dụng Material
		/// đúng hệ khi skill visual đang active.
		/// </summary>
		private void ApplyCurrentElementSlotMaterials()
		{
			RemoveElementSlotMaterials();

			if (!followPlayerSkill
				|| skeletonRenderer == null
				|| skeletonRenderer.skeleton == null
				|| !hasActiveSkill
				|| elementSlotMaterials == null)
			{
				return;
			}

			for (int i = 0; i < elementSlotMaterials.Count; i++)
			{
				ElementSlotMaterialOverride elementOverride =
					elementSlotMaterials[i];

				if (elementOverride == null
					|| elementOverride.overrideDisabled
					|| elementOverride.material == null
					|| string.IsNullOrEmpty(
						elementOverride.slotName
					))
				{
					continue;
				}

				if (elementOverride.elementType != currentSkillElement)
				{
					continue;
				}

				Slot slot =
					skeletonRenderer.skeleton.FindSlot(
						elementOverride.slotName
					);

				if (slot == null)
				{
					continue;
				}

				skeletonRenderer.CustomSlotMaterials[slot] =
					elementOverride.material;

				activeElementSlotMaterials[slot] =
					elementOverride.material;

				if (copyAttachmentMainTexture)
				{
					CopyAttachmentMainTexture(
						slot,
						elementOverride.material
					);
				}

				ForceSlotAlpha(slot);
			}
		}

		private void RemoveElementSlotMaterials()
		{
			if (activeElementSlotMaterials.Count == 0)
			{
				return;
			}

			if (skeletonRenderer == null
				|| skeletonRenderer.skeleton == null)
			{
				activeElementSlotMaterials.Clear();
				return;
			}

			foreach (
				KeyValuePair<Slot, Material> item
				in activeElementSlotMaterials
			)
			{
				Slot slot = item.Key;

				if (slot == null)
				{
					continue;
				}

				if (!skeletonRenderer.CustomSlotMaterials.TryGetValue(
					slot,
					out Material currentMaterial
				))
				{
					RestoreElementSlotAlpha(slot);
					continue;
				}

				/*
				 * Chỉ xóa nếu Material hiện tại đúng là
				 * Material do component này đã áp dụng.
				 */
				if (currentMaterial == item.Value)
				{
					skeletonRenderer.CustomSlotMaterials.Remove(
						slot
					);
				}

				RestoreElementSlotAlpha(slot);
			}

			activeElementSlotMaterials.Clear();
		}

		private void RefreshElementSlots()
		{
			if (activeElementSlotMaterials.Count == 0)
			{
				return;
			}

			foreach (
				KeyValuePair<Slot, Material> item
				in activeElementSlotMaterials
			)
			{
				Slot slot = item.Key;
				Material material = item.Value;

				if (slot == null || material == null)
				{
					continue;
				}

				ForceSlotAlpha(slot);

				if (copyAttachmentMainTexture)
				{
					CopyAttachmentMainTexture(
						slot,
						material
					);
				}
			}
		}

		/// <summary>
		/// Kiểm tra slot có được quản lý bởi danh sách Material theo hệ không.
		///
		/// Nếu có, customSlotMaterials thông thường sẽ không được phép
		/// ghi đè lên slot này.
		/// </summary>
		private bool IsElementManagedSlotName(
			string slotName
		)
		{
			if (!followPlayerSkill
				|| elementSlotMaterials == null
				|| string.IsNullOrEmpty(slotName))
			{
				return false;
			}

			for (int i = 0; i < elementSlotMaterials.Count; i++)
			{
				ElementSlotMaterialOverride elementOverride =
					elementSlotMaterials[i];

				if (elementOverride == null
					|| elementOverride.overrideDisabled
					|| string.IsNullOrEmpty(
						elementOverride.slotName
					))
				{
					continue;
				}

				if (elementOverride.slotName == slotName)
				{
					return true;
				}
			}

			return false;
		}

		#endregion

		#region Material Overrides

		private void SetCustomMaterialOverrides()
		{
			if (skeletonRenderer == null)
			{
				return;
			}

			for (int i = 0; i < customMaterialOverrides.Count; i++)
			{
				AtlasMaterialOverride atlasMaterialOverride =
					customMaterialOverrides[i];

				if (atlasMaterialOverride.overrideDisabled
					|| atlasMaterialOverride.originalMaterial == null
					|| atlasMaterialOverride.replacementMaterial == null)
				{
					continue;
				}

				skeletonRenderer.CustomMaterialOverride[
					atlasMaterialOverride.originalMaterial
				] = atlasMaterialOverride.replacementMaterial;
			}
		}

		private void RemoveCustomMaterialOverrides()
		{
			if (skeletonRenderer == null)
			{
				return;
			}

			for (int i = 0; i < customMaterialOverrides.Count; i++)
			{
				AtlasMaterialOverride atlasMaterialOverride =
					customMaterialOverrides[i];

				if (atlasMaterialOverride.originalMaterial == null)
				{
					continue;
				}

				if (!skeletonRenderer.CustomMaterialOverride.TryGetValue(
					atlasMaterialOverride.originalMaterial,
					out Material currentMaterial
				))
				{
					continue;
				}

				if (currentMaterial
					!= atlasMaterialOverride.replacementMaterial)
				{
					continue;
				}

				skeletonRenderer.CustomMaterialOverride.Remove(
					atlasMaterialOverride.originalMaterial
				);
			}
		}

		#endregion

		#region Attachment Texture

		private void CopyAttachmentMainTexture(
			Slot slot
		)
		{
			if (skeletonRenderer == null)
			{
				return;
			}

			if (!skeletonRenderer.CustomSlotMaterials.TryGetValue(
				slot,
				out Material overrideMaterial
			))
			{
				return;
			}

			CopyAttachmentMainTexture(
				slot,
				overrideMaterial
			);
		}

		private void CopyAttachmentMainTexture(
			Slot slot,
			Material overrideMaterial
		)
		{
			if (slot == null
				|| overrideMaterial == null
				|| !overrideMaterial.HasProperty(MainTexId))
			{
				return;
			}

			IHasTextureRegion textureRegionAttachment =
				slot.Attachment as IHasTextureRegion;

			if (textureRegionAttachment == null
				|| textureRegionAttachment.Region == null)
			{
				return;
			}

			AtlasRegion atlasRegion =
				textureRegionAttachment.Region as AtlasRegion;

			if (atlasRegion == null
				|| atlasRegion.page == null)
			{
				return;
			}

			Material sourceMaterial =
				atlasRegion.page.rendererObject as Material;

			if (sourceMaterial == null
				|| sourceMaterial.mainTexture == null)
			{
				return;
			}

			Texture sourceTexture =
				sourceMaterial.mainTexture;

			if (overrideMaterial.GetTexture(MainTexId)
				!= sourceTexture)
			{
				overrideMaterial.SetTexture(
					MainTexId,
					sourceTexture
				);
			}
		}

		#endregion

		#region Slot Alpha

		private void ForceSlotAlpha(Slot slot)
		{
			if (!ignoreSlotAlpha || slot == null)
			{
				return;
			}

			if (!originalSlotAlpha.ContainsKey(slot))
			{
				originalSlotAlpha.Add(slot, slot.A);
			}

			slot.A = 1f;
		}

		private void RestoreElementSlotAlpha(
			Slot slot
		)
		{
			if (slot == null || IsStaticCustomSlot(slot))
			{
				return;
			}

			if (!originalSlotAlpha.TryGetValue(
				slot,
				out float originalAlpha
			))
			{
				return;
			}

			slot.A = originalAlpha;
			originalSlotAlpha.Remove(slot);
		}

		private bool IsStaticCustomSlot(
			Slot slot
		)
		{
			if (slot == null
				|| skeletonRenderer == null
				|| skeletonRenderer.skeleton == null)
			{
				return false;
			}

			for (int i = 0; i < customSlotMaterials.Count; i++)
			{
				SlotMaterialOverride slotMaterialOverride =
					customSlotMaterials[i];

				if (slotMaterialOverride.overrideDisabled
					|| string.IsNullOrEmpty(
						slotMaterialOverride.slotName
					))
				{
					continue;
				}

				if (IsElementManagedSlotName(
					slotMaterialOverride.slotName
				))
				{
					continue;
				}

				Slot staticSlot =
					skeletonRenderer.skeleton.FindSlot(
						slotMaterialOverride.slotName
					);

				if (staticSlot == slot)
				{
					return true;
				}
			}

			return false;
		}

		private void RestoreSlotAlpha()
		{
			foreach (
				KeyValuePair<Slot, float> item
				in originalSlotAlpha
			)
			{
				if (item.Key != null)
				{
					item.Key.A = item.Value;
				}
			}

			originalSlotAlpha.Clear();
		}

		#endregion
	}
}
