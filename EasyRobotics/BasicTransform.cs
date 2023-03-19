using System;
using System.Collections.Generic;
using UnityEngine;

namespace EasyRobotics
{
    /// <summary>
    /// Unity Transform alike class that support only position and rotation (not scale)
    /// and a no-siblings parent-child hierarchy (a transform can only have a single child)
    /// </summary>
    public class BasicTransform
    {
        private Vector3 _localPosition;
        private Quaternion _localRotation = Quaternion.identity;

        private Vector3 _worldPosition;
        private Quaternion _worldRotation = Quaternion.identity;

        private bool _worldIsDirty;

        private BasicTransform _parent;

        /// <summary>
        /// Shared reference to all transforms in a parent-child chain, with the root as first item
        /// </summary>
        private List<BasicTransform> _chain;

        /// <summary>
        /// Instantiate a transform with the specified parent (or null to make it a root transform)
        /// </summary>
        public BasicTransform(BasicTransform parent)
        {
            SetParent(parent, out _);
            _worldIsDirty = true;
        }

        /// <summary>
        /// Instantiate a transform with the specified parent (or null to make it a root transform),
        /// with a given local or world position and rotation
        /// </summary>
        /// <param name="parent">the parent of this transform (or null to make it a root transform)</param>
        /// <param name="position">world or local position</param>
        /// <param name="rotation">world or local rotation</param>
        /// <param name="isLocalPosRot">if true, position/rotation are local (relative to parent), if false, position/rotation are in world space</param>
        public BasicTransform(BasicTransform parent, Vector3 position, Quaternion rotation, bool isLocalPosRot = false)
        {
            SetParent(parent, out _);

            if (isLocalPosRot || parent == null)
            {
                _localPosition = position;
                _localRotation = rotation;
            }
            else
            {
                _localPosition = position - parent.Position;
                _localRotation = rotation * parent.Rotation.Inverse();
            }

            _localRotation.Normalize();

            _worldIsDirty = true;
        }

        /// <summary>
        /// Position relative to parent
        /// </summary>
        public Vector3 LocalPosition
        {
            get => _localPosition;
            set
            {
                _localPosition = value;
                _worldIsDirty = true;
            }
        }

        /// <summary>
        /// Rotation relative to parent
        /// </summary>
        public Quaternion LocalRotation
        {
            get => _localRotation;
            set
            {
                _localRotation = value.normalized;
                _worldIsDirty = true;
            }
        }

        /// <summary>
        /// Absolute world position
        /// </summary>
        public Vector3 Position
        {
            get
            {
                UpdateWorldPosAndRot();
                return _worldPosition;
            }
            set
            {
                if (_parent == null)
                    _localPosition = value;
                else
                    _localPosition = _parent.Rotation.Inverse() * (value - _parent.Position);

                _worldIsDirty = true;
            }
        }

        /// <summary>
        /// Absolute world rotation
        /// </summary>
        public Quaternion Rotation
        {
            get
            {
                UpdateWorldPosAndRot();
                return _worldRotation;
            }
            set
            {
                if (_parent == null)
                    _localRotation = value;
                else
                    _localRotation = _parent.Rotation.Inverse() * value;

                _localRotation.Normalize();

                _worldIsDirty = true;
            }
        }

        public void GetPosAndRot(out Vector3 position, out Quaternion rotation)
        {
            UpdateWorldPosAndRot();
            position = _worldPosition;
            rotation = _worldRotation;
        }

        public Vector3 Up => Rotation * Vector3.up;
        public Vector3 Right => Rotation * Vector3.right;
        public Vector3 Forward => Rotation * Vector3.forward;


        /// <summary>
        /// Current parent.
        /// </summary>
        public BasicTransform Parent => _parent;

        public bool HasChain => _chain != null && _chain.Count > 0;

        public BasicTransform Root => _chain == null ? this : _chain[0];

        public BasicTransform Tip => _chain == null ? this : _chain[_chain.Count - 1];

        /// <summary>
        /// Set parent, reseting local position/rotation to zero
        /// </summary>
        public void SetParent(BasicTransform parent)
        {
            if (SetParent(parent, out _))
            {
                _localPosition = Vector3.zero;
                _localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Set parent, keeping the current world position/rotation
        /// </summary>
        public void SetParentKeepWorldPosAndRot(BasicTransform parent)
        {
            UpdateWorldPosAndRot();
            Vector3 worldPos = _worldPosition;
            Quaternion worldRot = _worldRotation;
            if (SetParent(parent, out _))
            {
                SetPosAndRot(worldPos, worldRot);
            }

        }

        public void SetPosAndRot(Vector3 position, Quaternion rotation)
        {
            if (_parent == null)
            {
                _localPosition = position;
                _localRotation = rotation.normalized;
            }
            else
            {
                Quaternion parentInverseRotation = _parent.Rotation.Inverse();
                _localPosition = parentInverseRotation * (position - _parent.Position);
                _localRotation = parentInverseRotation * rotation;
                _localRotation.Normalize();
            }

            _worldIsDirty = true;
        }

        private int ChainIndex
        {
            get
            {
                if (_chain == null)
                    return 0;

                for (int i = _chain.Count; i-- > 0;)
                    if (_chain[i] == this)
                        return i;

                throw new Exception("Invalid chain state");
            }
        }

        private bool SetParent(BasicTransform newParent, out BasicTransform detachedRoot)
        {
            detachedRoot = null;

            if (_parent == newParent)
                return false;

            if (newParent != null)
            {
                List<BasicTransform> newParentChain;
                // if parent is a root with no child, it might not have a chain, so instantiate it
                if (newParent._chain == null)
                {
                    newParentChain = new List<BasicTransform> { newParent };
                    newParent._chain = newParentChain;
                }
                // if parent has child(s), we need to detach them as a new hierarchy
                else
                {
                    newParentChain = newParent._chain;
                    int parentIndex = newParent.ChainIndex;
                    if (parentIndex < newParentChain.Count - 1)
                    {
                        int newRootIndex = parentIndex + 1;
                        int childCount = newParentChain.Count - newRootIndex;
                        List<BasicTransform> newChain = newParentChain.GetRange(newRootIndex, childCount);
                        newParentChain.RemoveRange(newRootIndex, childCount);
                        foreach (BasicTransform newChainItem in newChain)
                            newChainItem._chain = newChain;
                        detachedRoot = newChain[0];
                        detachedRoot._parent = null;
                    }
                }

                // if this is a root with no child, just add it at the end of the parent chain
                if (_chain == null)
                {
                    newParentChain.Add(this);
                    _chain = newParentChain;
                }
                // else move this and all childs from the current chain to the new parent chain
                else
                {
                    int chainIndex = ChainIndex;
                    List<BasicTransform> currentChain = _chain;
                    for (int i = chainIndex; i < currentChain.Count; i++)
                    {
                        BasicTransform transformToMove = currentChain[i];
                        newParentChain.Add(transformToMove);
                        transformToMove._chain = newParentChain;
                    }
                    currentChain.RemoveRange(chainIndex, currentChain.Count - chainIndex);

                }
            }
            // if new parent is null and this has a parent, detach ourselves and all childs in a new
            // chain and remove them from the parent chain.
            else if (_chain != null && _parent != null)
            {
                List<BasicTransform> parentChain = _chain;
                int chainIndex = ChainIndex;
                int childCount = parentChain.Count - chainIndex;
                List<BasicTransform> newChain = parentChain.GetRange(chainIndex, childCount);
                foreach (BasicTransform newChainItem in newChain)
                    newChainItem._chain = newChain;
                parentChain.RemoveRange(chainIndex, childCount);
            }

            _parent = newParent;
            _worldIsDirty = true;

            return true;
        }

        private void UpdateWorldPosAndRot()
        {
            bool hasChain = _chain != null;
            BasicTransform root = hasChain ? _chain[0] : this;
            bool isDirty = root._worldIsDirty;
            if (isDirty)
            {
                root._worldPosition = root._localPosition;
                root._worldRotation = root._localRotation;
                root._worldIsDirty = false;
            }

            if (!hasChain)
                return;

            int count = _chain.Count;
            for (int i = 1; i < count; i++)
            {
                BasicTransform current = _chain[i];
                if (isDirty || current._worldIsDirty)
                {
                    isDirty = true;
                    BasicTransform parent = current._parent;
                    current._worldPosition = parent._worldPosition + parent._worldRotation * current._localPosition;
                    current._worldRotation = parent._worldRotation * current._localRotation;
                    current._worldRotation.Normalize();
                    current._worldIsDirty = false;
                }
            }
        }
    }
}
