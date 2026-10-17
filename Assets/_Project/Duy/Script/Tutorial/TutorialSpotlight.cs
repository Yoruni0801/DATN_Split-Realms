using UnityEngine;
using UnityEngine.UI;


[RequireComponent(typeof(CanvasRenderer))]
    public class TutorialSpotlight : Graphic, ICanvasRaycastFilter
    {
        [SerializeField]
        private Rect m_CenterRect;

        public Rect centerRect
        {
            get { return m_CenterRect; }
            set
            {
                if (m_CenterRect != value)
                {
                    m_CenterRect = value;
                    SetVerticesDirty(); 
                }
            }
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect outer = GetPixelAdjustedRect();

            AddQuad(vh, new Rect(outer.xMin, outer.yMin, m_CenterRect.xMin - outer.xMin, outer.height));
            AddQuad(vh, new Rect(m_CenterRect.xMax, outer.yMin, outer.xMax - m_CenterRect.xMax, outer.height));
            AddQuad(vh, new Rect(m_CenterRect.xMin, m_CenterRect.yMax, m_CenterRect.width, outer.yMax - m_CenterRect.yMax));
            AddQuad(vh, new Rect(m_CenterRect.xMin, outer.yMin, m_CenterRect.width, m_CenterRect.yMin - outer.yMin));
        }

        private void AddQuad(VertexHelper vh, Rect rect)
        {
            if (rect.width <= 0 || rect.height <= 0) return;

            int startIndex = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;

            vert.position = new Vector3(rect.xMin, rect.yMin);
            vh.AddVert(vert);

            vert.position = new Vector3(rect.xMin, rect.yMax);
            vh.AddVert(vert);

            vert.position = new Vector3(rect.xMax, rect.yMax);
            vh.AddVert(vert);

            vert.position = new Vector3(rect.xMax, rect.yMin);
            vh.AddVert(vert);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, sp, eventCamera, out Vector2 localPoint);
            
            if (m_CenterRect.Contains(localPoint))
            {
                return false; 
            }
            return true; 
        }
    }