// Copyright 2022-2023 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
#if OCULUS_SUPPORTED
        [Header("Meta Passthrough")]
        [SerializeField] private bool m_EnableEdgeRendering = true;
        [SerializeField] private Color m_EdgeColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float m_TextureOpacity = 1f;

        private OVRPassthroughLayer m_PassthroughLayer;
#endif

        void Start()
        {
#if OCULUS_SUPPORTED
            m_PassthroughLayer = gameObject.AddComponent<OVRPassthroughLayer>();
            m_PassthroughLayer.overlayType = OVROverlay.OverlayType.Underlay;
            m_PassthroughLayer.textureOpacity = m_TextureOpacity;
            m_PassthroughLayer.edgeRenderingEnabled = m_EnableEdgeRendering;
            m_PassthroughLayer.edgeColor = m_EdgeColor;
            App.VrSdk.m_OvrManager.shouldBoundaryVisibilityBeSuppressed = true;
#endif // OCULUS_SUPPORTED
        }

        public void SetEdgeRendering(bool enabled)
        {
#if OCULUS_SUPPORTED
            m_EnableEdgeRendering = enabled;
            if (m_PassthroughLayer != null)
            {
                m_PassthroughLayer.edgeRenderingEnabled = enabled;
            }
#endif
        }

        public void SetEdgeColor(Color color)
        {
#if OCULUS_SUPPORTED
            m_EdgeColor = color;
            if (m_PassthroughLayer != null)
            {
                m_PassthroughLayer.edgeColor = color;
            }
#endif
        }

        public void SetTextureOpacity(float opacity)
        {
#if OCULUS_SUPPORTED
            m_TextureOpacity = Mathf.Clamp01(opacity);
            if (m_PassthroughLayer != null)
            {
                m_PassthroughLayer.textureOpacity = m_TextureOpacity;
            }
#endif
        }

        void OnDestroy()
        {
#if OCULUS_SUPPORTED
            App.VrSdk.m_OvrManager.shouldBoundaryVisibilityBeSuppressed = false;
#endif
        }
    }
}
