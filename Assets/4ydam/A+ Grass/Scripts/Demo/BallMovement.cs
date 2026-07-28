using UnityEngine;
using UnityEngine.InputSystem;

public class BallMovement : MonoBehaviour
{
	[SerializeField] private float moveForce = 25f;
	[SerializeField] private float rollTorque = 18f;
	[SerializeField] private float maxSpeed = 8f;

	private Rigidbody _rb;
	private Vector2 _moveInput;

	private void Awake()
	{
		_rb = GetComponent<Rigidbody>();
	}

	private void Update()
	{
		_moveInput = ReadMoveInput();
	}

	private void FixedUpdate()
	{
		Vector3 input = new Vector3(_moveInput.x, 0f, _moveInput.y);

		if (input.sqrMagnitude > 1f)
		{
			input.Normalize();
		}

		if (input.sqrMagnitude <= 0f)
		{
			return;
		}

		_rb.AddForce(input * moveForce, ForceMode.Acceleration);

		Vector3 torqueAxis = Vector3.Cross(Vector3.up, input);
		_rb.AddTorque(torqueAxis * rollTorque, ForceMode.Acceleration);

		Vector3 horizontalVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
		if (horizontalVelocity.magnitude > maxSpeed)
		{
			Vector3 clampedHorizontal = horizontalVelocity.normalized * maxSpeed;
			_rb.linearVelocity = new Vector3(clampedHorizontal.x, _rb.linearVelocity.y, clampedHorizontal.z);
		}
	}

	private static Vector2 ReadMoveInput()
	{
		Vector2 input = Vector2.zero;

		if (Keyboard.current != null)
		{
			if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
			if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
			if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
			if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
		}

		if (Gamepad.current != null)
		{
			Vector2 stickInput = Gamepad.current.leftStick.ReadValue();
			if (stickInput.sqrMagnitude > input.sqrMagnitude)
			{
				input = stickInput;
			}
		}

		return input;
	}
}
