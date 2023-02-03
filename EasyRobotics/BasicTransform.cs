using System.Runtime.CompilerServices;
using UnityEngine;

namespace EasyRobotics
{
    /// <summary>
    /// Unity Transform alike class that support only pos/rot (not scale) and a no-siblings
    /// parent-child hierarchy (each transform can only have one child transform)
    /// </summary>
    public class BasicTransform
    {
        private Vector3 _localPosition;
        private Quaternion _localRotation;

        private Vector3 _worldPosition;
        private Quaternion _worldRotation;

        private bool _worldIsDirty;

        private BasicTransform _parent;
        private BasicTransform _child;

        /// <summary>
        /// Instantiate a transform with the specified parent (or null to make it a root transform)
        /// </summary>
        public BasicTransform(BasicTransform parent)
        {
            Parent = parent;
            SetDirty();
        }

        /// <summary>
        /// Instantiate a transform with the specified parent (or null to make it a root transform),
        /// with a given local or world position and rotation
        /// </summary>
        /// <param name="parent">the parent of thsi transform (or null to make it a root transform)</param>
        /// <param name="position">world or local position</param>
        /// <param name="rotation">world or local rotation</param>
        /// <param name="isLocalPosRot">if true, position/rotation are local (relative to parent), if false, position/rotation are in world space</param>
        public BasicTransform(BasicTransform parent, Vector3 position, Quaternion rotation, bool isLocalPosRot = false)
        {
            Parent = parent;

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

            SetDirty();
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
                SetDirty();
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
                SetDirty();
            }
        }

        /// <summary>
        /// Absolute world position
        /// </summary>
        public Vector3 Position
        {
            get
            {
                //if (_worldIsDirty)
                    UpdateWorldPosAndRot();

                return _worldPosition;
            }
            set
            {
                if (_parent == null)
                    _localPosition = value;
                else
                    _localPosition = _parent.Rotation.Inverse() * (value - _parent.Position);

                SetDirty();
            }
        }

        /// <summary>
        /// Absolute world rotation
        /// </summary>
        public Quaternion Rotation
        {
            get
            {
                //if (_worldIsDirty)
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

                SetDirty();
            }
        }

        public Vector3 Up => Rotation * Vector3.up;
        public Vector3 Right => Rotation * Vector3.right;
        public Vector3 Forward => Rotation * Vector3.forward;


        /// <summary>
        /// Current parent. Setter will reset local position/rotation to zero/identity
        /// </summary>
        public BasicTransform Parent
        {
            get
            {
                return _parent;
            }
            set
            {
                if (_parent == value)
                    return;

                // if parent already had a child, detach it, keeping its world pos
                if (_parent?._child != null)
                {
                    BasicTransform previousChild = _parent._child;
                    Vector3 childPos = previousChild.Position;
                    Quaternion childRot = previousChild.Rotation;
                    previousChild._parent = null;
                    previousChild.Position = childPos;
                    previousChild.Rotation = childRot;
                }

                _parent = value;
                if (value != null)
                    _parent._child = this;

                _localRotation = Quaternion.identity;
                _localPosition = Vector3.zero;

                SetDirty();
            }
        }

        /// <summary>
        /// Set parent, keeping the current world position/rotation
        /// </summary>
        public void SetParentKeepWorldPosAndRot(BasicTransform parent)
        {
            Vector3 worldPos = Position;
            Quaternion worldRot = Rotation;
            Parent = parent;
            Position = worldPos;
            Rotation = worldRot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDirtyIfNeeded()
        {
            if (_worldIsDirty)
                return;

            SetDirty();
        }

        private void SetDirty()
        {
            _worldIsDirty = true;

            BasicTransform child = _child;
            while (child != null)
            {
                _child._worldIsDirty = true;
                child = child._child;
            }
        }

        private void UpdateWorldPosAndRot()
        {
            BasicTransform next = this;
            while (next._parent != null)
            {
                next = next._parent;
            }

            bool isDirty = next._worldIsDirty;

            while(true)
            {
                //if (isDirty || next._worldIsDirty)
                //{
                    isDirty = true;
                    BasicTransform parent = next._parent;
                    if (parent == null)
                    {
                        next._worldPosition = next._localPosition;
                        next._worldRotation = next._localRotation;
                    }
                    else
                    {
                        next._worldPosition = parent._worldPosition + parent._worldRotation * next._localPosition;
                        next._worldRotation = parent._worldRotation * next._localRotation;
                        next._worldRotation.Normalize();
                    }

                    next._worldIsDirty = false;
                //}

                if (next == this)
                    return;

                next = next._child;
            }
        }
    }
}
