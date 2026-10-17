using System;

namespace Quoc.QuyVuong
{
    /// <summary>
    /// Interface cho các script Phase Boss (Phase 1, Phase 2, Phase 3...) để thông báo khi hoàn thành 1 flow hành động.
    /// </summary>
    public interface IQuyVuongPhaseFlow
    {
        /// <summary>
        /// Sự kiện phát ra khi Phase vừa hoàn thành xong 1 flow (1 chuỗi combo / lượt hành động).
        /// </summary>
        event Action OnFlowCompleted;

        /// <summary>
        /// Trạng thái kiểm tra xem Phase có đang trong quá trình thực thi flow hay không.
        /// </summary>
        bool IsExecutingFlow { get; }

        /// <summary>
        /// Đóng băng Boss: Dừng di chuyển, dừng target Player, dừng tấn công.
        /// Dùng khi chuyển phase để Boss đứng yên chờ transition VFX.
        /// </summary>
        void SetFrozen(bool frozen);

        /// <summary>
        /// Hủy và dọn dẹp tất cả kỹ năng/đạn/vật thể đã được sinh ra trên sân (dùng khi boss chết hoặc reset).
        /// </summary>
        void ClearAllSpawnedSkills();
    }
}
