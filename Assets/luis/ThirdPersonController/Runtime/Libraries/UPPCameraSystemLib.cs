using UnityEngine;


namespace UPP.ThirdPersonController.CameraSystems
{

	#region Estado base de cámara
	[System.Serializable]
	public class CameraState
	{
		[Header("Configuración")]
		public string StateName = "Estado de cámara";
		public float Distance;
		public float MovementSpeed;

		[Header("Campo de visión")]
		public float CameraFieldOfView;

		[Header("Desplazamiento del pivote")]
		public float UpTargetOffset = 1;
		public float RightTargetOffset = 0;
		public float ForwardTargetOffset;


		[Header("Ajuste de posición")]
		public float RightCameraOffset = 0.6f;
		public float UpCameraOffset = 0.45f;
		public float ForwardCameraOffset = 0f;

		[Header("Rotación de cámara")]
		public float RotationSensibility = 1f;
		public float VerticalRotationSensibility = 0.7f;

		public float MaxRotation = -80;
		public float MinRotation = 80;

		[Header("Capas de colisión")]
		public LayerMask CollisionLayers;

		public Vector3 GetCameraPivotPosition(Transform target)
		{
			return (target != null) ? target.position + target.up * UpTargetOffset + target.forward * ForwardTargetOffset + target.right * RightTargetOffset : Vector3.zero;
		}
		public Vector3 GetCameraPosition(Transform camera)
		{
			return camera.parent.position - camera.forward * (Distance + ForwardCameraOffset) + camera.right * RightCameraOffset + camera.up * UpCameraOffset;
		}

		public CameraState(string stateName, float distance = 3, float movementSpeed = 15, float cameraFielOfView = 60, float upOffset = 0f, float rightOffset = 0, float forwardOffset = 0f, float xAdjust = 0.6f, float yAdjust = 0.6f, float zAdjust = 0, float rotationSensibility = 5f, float minRotation = -80, float maxRotation = 80)
		{
			StateName = stateName;
			Distance = distance;
			MovementSpeed = movementSpeed;

			CameraFieldOfView = cameraFielOfView;

			UpTargetOffset = upOffset;
			RightTargetOffset = rightOffset;
			ForwardTargetOffset = forwardOffset;

			RightCameraOffset = xAdjust;
			UpCameraOffset = yAdjust;
			ForwardCameraOffset = zAdjust;

			RotationSensibility = rotationSensibility;
			MinRotation = minRotation;
			MaxRotation = maxRotation;

			SettingsIDName = stateName;
		}

		[HideInInspector] public string SettingsIDName;
	}
	#endregion

	public class UPPCameraController : MonoBehaviour
	{
		[HideInInspector] public bool Aiming;
		[HideInInspector] public bool IsTransitioningToCustomState;
		[HideInInspector] public Camera mCamera;

		[Header("Configuración de cámara")]
		public Transform TargetToFollow;
		public LayerMask CameraCollisionLayerMask;

		public bool LockCursor = true;
		public bool HideCursor = true;
		
		public CameraState[] CustomCameraStates = new CameraState[] { new CameraState("Estado de ejemplo", distance: 2, cameraFielOfView: 80) };

		
		private CameraState CurrentCameraState = new CameraState("Cámara estándar");
		public CameraState GetCurrentCameraState { get => CurrentCameraState; }


		#region Rotación de cámara
		[Header("Rotación de cámara")]
		[Range(0, 5)] public float GeneralSensibility = 1;
		[Range(0, 5)] public float GeneralVerticalSensibility = 1;
		public bool InvertHorizontal;
		public bool InvertVertical;

		
		[HideInInspector] public float rotX;
		[HideInInspector] public float rotY;

		
		[HideInInspector] public float rotxtarget;
		[HideInInspector] public float rotytarget;
		#endregion

		#region Eventos
#pragma warning disable 67
		public static event System.Action event_OnCameraRotate;
		public static event System.Action event_OnCameraMove;
		public static event System.Action event_OnCameraStateChange;
#pragma warning restore 67
		#endregion

		protected virtual void OnEnable()
		{
			
			event_OnCameraRotate += OnCameraRotate;
			event_OnCameraMove += OnCameraMove;
			event_OnCameraStateChange += OnCameraStateChange;
		}
		protected virtual void OnDestroy()
		{
			
			event_OnCameraRotate -= OnCameraRotate;
			event_OnCameraMove -= OnCameraMove;
			event_OnCameraStateChange -= OnCameraStateChange;
		}

		protected virtual void Start()
		{
			mCamera ??= gameObject.GetComponentInChildren<Camera>();
			if (TargetToFollow == null)
			{
				GameObject player = GameObject.FindGameObjectWithTag("Player");
				if (player != null)
				{
					TargetToFollow = player.transform;
				}
			}

			if (TargetToFollow != null)
			{
				SetCameraRotation(0, TargetToFollow.eulerAngles.y, false);
			}

			LockMouse(LockCursor, HideCursor);
		}


		protected virtual void OnCameraRotate()
		{

		}
		protected virtual void OnCameraMove()
		{

		}
		protected virtual void OnCameraStateChange()
		{

		}

				public void SetCameraStateTransition(CameraState current, CameraState target, float speed = 8, bool lerp = true)
		{
			speed = Mathf.Clamp01(speed * Time.deltaTime);
			if (speed != -1)
			{
				switch (lerp)
				{
					case true:
						current.Distance = Mathf.Lerp(current.Distance, target.Distance, speed);
						current.MovementSpeed = Mathf.Lerp(current.MovementSpeed, target.MovementSpeed, speed);

						current.UpTargetOffset = Mathf.Lerp(current.UpTargetOffset, target.UpTargetOffset, speed);
						current.ForwardTargetOffset = Mathf.Lerp(current.ForwardTargetOffset, target.ForwardTargetOffset, speed);
						current.RightTargetOffset = Mathf.Lerp(current.RightTargetOffset, target.RightTargetOffset, speed);

						current.CameraFieldOfView = Mathf.Lerp(current.CameraFieldOfView, target.CameraFieldOfView, speed);

						current.RightCameraOffset = Mathf.Lerp(current.RightCameraOffset, target.RightCameraOffset, speed);
						current.UpCameraOffset = Mathf.Lerp(current.UpCameraOffset, target.UpCameraOffset, speed);
						current.ForwardCameraOffset = Mathf.Lerp(current.ForwardCameraOffset, target.ForwardCameraOffset, speed);

						current.RotationSensibility = Mathf.Lerp(current.RotationSensibility, target.RotationSensibility, speed);
						current.VerticalRotationSensibility = Mathf.Lerp(current.VerticalRotationSensibility, target.VerticalRotationSensibility, speed);

						current.MaxRotation = Mathf.Lerp(current.MaxRotation, target.MaxRotation, speed);
						current.MinRotation = Mathf.Lerp(current.MinRotation, target.MinRotation, speed);
						break;
					case false:
						current.Distance = Mathf.MoveTowards(current.Distance, target.Distance, speed);
						current.MovementSpeed = Mathf.MoveTowards(current.MovementSpeed, target.MovementSpeed, speed);

						current.UpTargetOffset = Mathf.MoveTowards(current.UpTargetOffset, target.UpTargetOffset, speed);
						current.ForwardTargetOffset = Mathf.MoveTowards(current.ForwardTargetOffset, target.ForwardTargetOffset, speed);
						current.RightTargetOffset = Mathf.MoveTowards(current.RightTargetOffset, target.RightTargetOffset, speed);

						current.CameraFieldOfView = Mathf.MoveTowards(current.CameraFieldOfView, target.CameraFieldOfView, speed);

						current.RightCameraOffset = Mathf.MoveTowards(current.RightCameraOffset, target.RightCameraOffset, speed);
						current.UpCameraOffset = Mathf.MoveTowards(current.UpCameraOffset, target.UpCameraOffset, speed);
						current.ForwardCameraOffset = Mathf.MoveTowards(current.ForwardCameraOffset, target.ForwardCameraOffset, speed);

						current.RotationSensibility = Mathf.MoveTowards(current.RotationSensibility, target.RotationSensibility, speed);
						current.VerticalRotationSensibility = Mathf.MoveTowards(current.VerticalRotationSensibility, target.VerticalRotationSensibility, speed);

						current.MaxRotation = Mathf.MoveTowards(current.MaxRotation, target.MaxRotation, speed);
						current.MinRotation = Mathf.MoveTowards(current.MinRotation, target.MinRotation, speed);
						break;
				}
			}
			else
			{
				current.Distance = target.Distance;
				current.MovementSpeed = target.MovementSpeed;

				current.UpTargetOffset = target.UpTargetOffset;
				current.ForwardTargetOffset = target.ForwardTargetOffset;
				current.RightTargetOffset = target.RightTargetOffset;

				current.CameraFieldOfView = target.CameraFieldOfView;

				current.RightCameraOffset = target.RightCameraOffset;
				current.UpCameraOffset = target.UpCameraOffset;
				current.ForwardCameraOffset = target.ForwardCameraOffset;

				current.RotationSensibility = target.RotationSensibility;
				current.VerticalRotationSensibility = target.RotationSensibility;

				current.MaxRotation = target.MaxRotation;
				current.MinRotation = target.MinRotation;
			}
			
			if (current.Distance == target.Distance && current.MovementSpeed == target.MovementSpeed && current.SettingsIDName != target.SettingsIDName)
			{
				OnCameraStateChange();
				current.SettingsIDName = target.SettingsIDName;
			}


			current.CollisionLayers = target.CollisionLayers;
		}

				public void SetCustomCameraStateTransition(CameraState current, string customCameraStateName, float speed = 8)
		{
			CameraState SelectedState = null;
			foreach (CameraState s in CustomCameraStates) { if (s.StateName == customCameraStateName) SelectedState = s; }

			if (SelectedState == null) { Debug.LogWarning("No se encontró un estado de cámara con ese nombre. Verificá su configuración.", gameObject); return; }

			SetCameraStateTransition(current, SelectedState, speed);
			IsTransitioningToCustomState = true;
		}
		public void DisableCustomStateTransitioningState()
		{
			IsTransitioningToCustomState = false;
		}


				public virtual void SetPivotCameraPosition(Vector3 TargetPosition, bool SmoothMove = true, float Speed = 0)
		{
			if (transform.position != TargetPosition) { OnCameraMove(); }

			if (SmoothMove)
			{
				transform.position = Vector3.Lerp(transform.position, TargetPosition, (Speed != 0) ? Speed * Time.fixedDeltaTime : CurrentCameraState.MovementSpeed * Time.fixedDeltaTime);
			}
			else
			{
				transform.position = TargetPosition;
			}
		}

				public virtual void SetCameraPosition(Vector3 TargetPosition, bool SmoothMove = true, float Speed = 0)
		{
			if (mCamera.transform.position != TargetPosition) { OnCameraMove(); }
			if (SmoothMove)
			{
				mCamera.transform.position = Vector3.Lerp(transform.position, TargetPosition, (Speed != 0) ? Speed : CurrentCameraState.MovementSpeed * Time.fixedDeltaTime);
			}
			else
			{
				mCamera.transform.position = TargetPosition;
			}
		}



		public virtual void RotateCamera(float VerticalAxis, float HorizonalAxis, float LerpSpeed = 30, Vector3 upward = default(Vector3), Transform AlternativeTargetToCalculate = null, bool UseTimeScale = true)
		{
			if (mCamera == null
				|| mCamera.transform.parent == null
				|| TargetToFollow == null)
			{
				return;
			}

			if (upward.sqrMagnitude < 0.0001f)
			{
				upward = TargetToFollow.up;
			}

			if (VerticalAxis != 0 && HorizonalAxis != 0)
			{
				OnCameraRotate();
			}

			if (InvertVertical) VerticalAxis *= -1;
			if (InvertHorizontal) HorizonalAxis *= -1;

			
			rotxtarget -= (UseTimeScale ? Time.timeScale : 1) * GeneralVerticalSensibility * VerticalAxis * CurrentCameraState.VerticalRotationSensibility;
			
			rotytarget += (UseTimeScale ? Time.timeScale : 1) * GeneralSensibility * HorizonalAxis * CurrentCameraState.RotationSensibility;
			
			rotxtarget = Mathf.Clamp(rotxtarget, CurrentCameraState.MinRotation, CurrentCameraState.MaxRotation);

			
			rotX = Mathf.Lerp(rotX, rotxtarget, LerpSpeed * Time.fixedDeltaTime * (UseTimeScale ? Time.timeScale : 1));
			rotY = Mathf.Lerp(rotY, rotytarget, LerpSpeed * Time.fixedDeltaTime * (UseTimeScale ? Time.timeScale : 1));

			
			var rot = Quaternion.Euler(new Vector3(rotX, rotY, 0));

			if (AlternativeTargetToCalculate == null)
			{
				Quaternion targetRotation = TargetToFollow.root.rotation;
				targetRotation = Quaternion.AngleAxis(rotY, transform.up);
				targetRotation = Quaternion.AngleAxis(0, transform.forward);
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 50 * Time.deltaTime);
			}
			else
			{
				Quaternion targetRotation = AlternativeTargetToCalculate.rotation;
				targetRotation = Quaternion.AngleAxis(rotY, transform.up);
				targetRotation = Quaternion.AngleAxis(0, transform.forward);
				transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 50 * Time.deltaTime);
			}
			mCamera.transform.parent.localRotation = Quaternion.FromToRotation(transform.up, upward) * rot;
		}

				public virtual void SetCameraRotation(float Xtarget, float Ytarget, bool SmoothRotate = true)
		{
			if (SmoothRotate)
			{
				rotxtarget = Xtarget;
				rotytarget = Ytarget;
			}
			else
			{
				rotxtarget = Xtarget;
				rotytarget = Ytarget;
				rotX = Xtarget;
				rotY = Ytarget;
			}

			if (rotX != Xtarget && rotY != Ytarget)
			{
				OnCameraRotate();
			}
			

		}



				public virtual void SetCameraCollision(LayerMask CollisionLayer, bool Enabled = true)
		{
			if (Enabled == false) return;

			RaycastHit CameraCollisionHit;
			if (Physics.Linecast(transform.position, mCamera.transform.position, out CameraCollisionHit, CollisionLayer))
			{
				mCamera.transform.position = CameraCollisionHit.point + CameraCollisionHit.normal * 0.05f;
			}
		}

				public virtual void SetFieldOfView(float FOV)
		{
			if (mCamera.orthographic == true)
			{
				mCamera.orthographicSize = FOV / 10;
				mCamera.fieldOfView = FOV;
			}
			else
			{
				mCamera.fieldOfView = FOV;
			}
		}

				public static void LockMouse(bool Lock = true, bool Hide = true)
		{
			Cursor.lockState = Lock ? CursorLockMode.Locked : CursorLockMode.None;
			Cursor.visible = !Hide;
		}

	}

}
