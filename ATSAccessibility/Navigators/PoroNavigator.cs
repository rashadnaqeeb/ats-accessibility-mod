using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Navigator for Poro buildings (creature care).
	/// Poros extend Building (not ProductionBuilding) and have no workers.
	/// Provides Info, Happiness, Needs, and Product sections.
	/// </summary>
	public class PoroNavigator: BuildingSectionNavigator {
		// ========================================
		// SECTION TYPES
		// ========================================

		private enum SectionType {
			Info,
			Happiness,
			Needs,
			Product
		}

		// ========================================
		// CACHED DATA
		// ========================================

		private string[] _sectionNames;
		private SectionType[] _sectionTypes;
		private string _buildingName;
		private string _buildingDescription;

		// Happiness data
		private float _happiness;
		private float _productionProgress;

		// Needs data
		private List<NeedInfo> _needs = new List<NeedInfo>();

		// Product data
		private string _productName;
		private int _productAmount;
		private int _maxProducts;
		private bool _canGather;

		// ========================================
		// NEED INFO STRUCT
		// ========================================

		private struct NeedInfo {
			public int NeedIndex;
			public string NeedName;
			public float Level;
			public string CurrentGoodName;
			public int AvailableGoodsCount;
			public bool CanFulfill;
		}

		// ========================================
		// BASE CLASS IMPLEMENTATION
		// ========================================

		protected override string NavigatorName => "PoroNavigator";

		protected override string GetOpenAnnouncement() {
			if (!string.IsNullOrEmpty(_buildingDescription))
				return Strings.Get("nav.poro.open_with_desc", _buildingName, _buildingDescription);
			return _buildingName ?? Strings.Get("nav.poro.default_name");
		}

		protected override string[] GetSections() {
			return _sectionNames;
		}

		protected override int GetItemCount(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Info:
					return 0;
				case SectionType.Happiness:
					return 2;  // Happiness level, Production progress
				case SectionType.Needs:
					return _needs.Count > 0 ? _needs.Count : 1;
				case SectionType.Product:
					return 1;  // Product info (amount ready)
				default:
					return 0;
			}
		}

		protected override int GetSubItemCount(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			// Needs have sub-items (Feed, Change good options)
			if (_sectionTypes[sectionIndex] == SectionType.Needs && itemIndex < _needs.Count) {
				var need = _needs[itemIndex];
				int count = 0;
				if (need.CanFulfill) count++;  // Feed action
				if (need.AvailableGoodsCount > 1) count += need.AvailableGoodsCount;  // Good options
				return count;
			}

			// Product has sub-item (Collect action if products ready)
			if (_sectionTypes[sectionIndex] == SectionType.Product && _canGather) {
				return 1;  // Collect action
			}

			return 0;
		}

		protected override void AnnounceSection(int sectionIndex) {
			string sectionName = _sectionNames[sectionIndex];
			Speech.Say(sectionName);
		}

		protected override void AnnounceItem(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Happiness:
					AnnounceHappinessItem(itemIndex);
					break;
				case SectionType.Needs:
					AnnounceNeedItem(itemIndex);
					break;
				case SectionType.Product:
					AnnounceProductItem(itemIndex);
					break;
			}
		}

		protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Needs && itemIndex < _needs.Count) {
				var need = _needs[itemIndex];
				int subIndex = subItemIndex;

				// First sub-item is Feed action (if available)
				if (need.CanFulfill) {
					if (subIndex == 0) {
						Speech.Say(Strings.Get("nav.poro.feed"));
						return;
					}
					subIndex--;
				}

				// Remaining sub-items are good options
				if (need.AvailableGoodsCount > 1 && subIndex < need.AvailableGoodsCount) {
					string goodName = PoroReflection.GetNeedAvailableGoodName(_building, need.NeedIndex, subIndex);
					Speech.Say(Strings.Get("nav.poro.change_to", goodName ?? Strings.Get("common.unknown")));
				}
			} else if (_sectionTypes[sectionIndex] == SectionType.Product && _canGather) {
				Speech.Say(Strings.Get("nav.poro.collect_action", _productAmount, _productName ?? Strings.Get("nav.poro.products_default")));
			}
		}

		protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Needs && itemIndex < _needs.Count) {
				var need = _needs[itemIndex];
				int subIndex = subItemIndex;

				// First sub-item is Feed action (if available)
				if (need.CanFulfill) {
					if (subIndex == 0) {
						if (PoroReflection.FulfillNeed(_building, need.NeedIndex)) {
							Speech.Say(Strings.Get("nav.poro.fed_successfully"));
							RefreshNeedData();
							return true;
						} else {
							Speech.Say(Strings.Get("nav.poro.cannot_feed"));
							return false;
						}
					}
					subIndex--;
				}

				// Remaining sub-items are good options
				if (need.AvailableGoodsCount > 1 && subIndex < need.AvailableGoodsCount) {
					if (PoroReflection.ChangeNeedGood(_building, need.NeedIndex, subIndex)) {
						string goodName = PoroReflection.GetNeedAvailableGoodName(_building, need.NeedIndex, subIndex);
						Speech.Say(Strings.Get("nav.poro.changed_to", goodName ?? Strings.Get("common.unknown")));
						RefreshNeedData();
						return true;
					}
				}
			} else if (_sectionTypes[sectionIndex] == SectionType.Product && _canGather) {
				if (PoroReflection.GatherProducts(_building)) {
					Speech.Say(Strings.Get("nav.poro.collected", _productAmount, _productName ?? Strings.Get("nav.poro.products_default")));
					RefreshProductData();
					return true;
				} else {
					Speech.Say(Strings.Get("common.cannot_collect"));
					return false;
				}
			}
			return false;
		}

		protected override void RefreshData() {
			_buildingName = BuildingReflection.GetBuildingName(_building) ?? Strings.Get("nav.poro.default_name");
			_buildingDescription = BuildingReflection.GetBuildingDescription(_building);

			RefreshHappinessData();
			RefreshNeedData();
			RefreshProductData();
			BuildSections();

			Debug.Log($"[ATSAccessibility] PoroNavigator: Refreshed data - happiness {_happiness:P0}, {_needs.Count} needs");
		}

		protected override void ClearData() {
			_needs.Clear();
			_sectionNames = null;
			_sectionTypes = null;
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshHappinessData() {
			_happiness = PoroReflection.GetHappiness(_building);
			_productionProgress = PoroReflection.GetProductionProgress(_building);
		}

		private void RefreshNeedData() {
			_needs.Clear();

			int needCount = PoroReflection.GetNeedCount(_building);
			for (int i = 0; i < needCount; i++) {
				var need = new NeedInfo {
					NeedIndex = i,
					NeedName = PoroReflection.GetNeedName(_building, i),
					Level = PoroReflection.GetNeedLevel(_building, i),
					CurrentGoodName = PoroReflection.GetNeedCurrentGoodName(_building, i),
					AvailableGoodsCount = PoroReflection.GetNeedAvailableGoodsCount(_building, i),
					CanFulfill = PoroReflection.CanFulfillNeed(_building, i)
				};
				_needs.Add(need);
			}
		}

		private void RefreshProductData() {
			_productName = PoroReflection.GetProductName(_building);
			_productAmount = PoroReflection.GetProductAmount(_building);
			_maxProducts = PoroReflection.GetMaxProducts(_building);
			_canGather = PoroReflection.CanGatherProducts(_building);
		}

		private void BuildSections() {
			var sections = new List<string>();
			var types = new List<SectionType>();

			// Happiness section
			sections.Add(Strings.Get("nav.poro.section.happiness"));
			types.Add(SectionType.Happiness);

			// Needs section
			sections.Add(Strings.Get("nav.poro.section.needs"));
			types.Add(SectionType.Needs);

			// Product section
			sections.Add(Strings.Get("common.product"));
			types.Add(SectionType.Product);

			_sectionNames = sections.ToArray();
			_sectionTypes = types.ToArray();
		}

		// ========================================
		// HAPPINESS SECTION
		// ========================================

		private void AnnounceHappinessItem(int itemIndex) {
			if (itemIndex == 0) {
				Speech.Say(Strings.Get("nav.poro.happiness", $"{_happiness:P0}"));
			} else if (itemIndex == 1) {
				Speech.Say(Strings.Get("nav.poro.production_progress", $"{_productionProgress:P0}"));
			}
		}

		// ========================================
		// NEEDS SECTION
		// ========================================

		private void AnnounceNeedItem(int itemIndex) {
			if (_needs.Count == 0) {
				Speech.Say(Strings.Get("nav.poro.no_needs"));
				return;
			}

			if (itemIndex < _needs.Count) {
				var need = _needs[itemIndex];
				string needName = need.NeedName ?? Strings.Get("nav.poro.need_default", need.NeedIndex + 1);
				string levelPercent = $"{need.Level:P0}";
				string currentGood = need.CurrentGoodName ?? Strings.Get("common.unknown");

				Speech.Say(Strings.Get("nav.poro.need_line", needName, levelPercent, currentGood));
			}
		}

		// ========================================
		// PRODUCT SECTION
		// ========================================

		private void AnnounceProductItem(int itemIndex) {
			if (itemIndex == 0) {
				string productName = _productName ?? Strings.Get("common.product");
				string announcement = Strings.Get("nav.poro.product_ready", productName, _productAmount, _maxProducts);

				if (_productAmount == 0) {
					announcement += Strings.Get("nav.poro.product_none_suffix");
				}

				Speech.Say(announcement);
			}
		}
	}
}
