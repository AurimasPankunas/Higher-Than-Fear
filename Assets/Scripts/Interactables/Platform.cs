using System.Collections;
using UnityEngine;

public class Platform : MonoBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public float duration = 1f;
    public float loweredHeight = 1.5f;

    [Header("Move")]
    public Axis moveAxis = Axis.Y;
    public float moveDistance = 1.5f;

    [Header("Rotate")]
    public Axis rotationAxis = Axis.X;
    public float rotationAngle = 90f;
    public Vector3 rotationPivotOffset = new Vector3(0f, 0f, -0.5f);

    private bool _lowerDoor;
    private bool _moved;
    private bool _rotated;

    private Vector3 _raisedPosition;
    private Quaternion _startRotation;

    // Cache the starting pose so the public methods can be called safely.
    void Awake()
    {
        _raisedPosition = transform.position;
        _startRotation = transform.rotation;
    }

    public void ToggleDoorOpen()
    {
        StopAllCoroutines();
        if (!_lowerDoor)
        {
            Vector3 lowerPosition = _raisedPosition + Vector3.down * loweredHeight;
            StartCoroutine(MoveTo(lowerPosition));
        }
        else
        {
            StartCoroutine(MoveTo(_raisedPosition));
        }

        _lowerDoor = !_lowerDoor;
    }

    public void MovePlatform()
    {
        StopAllCoroutines();

        _moved = !_moved;
        Vector3 targetPosition = _moved
            ? _raisedPosition + AxisToDirection(moveAxis) * moveDistance
            : _raisedPosition;

        StartCoroutine(MoveTo(targetPosition));
    }

    public void RotatePlatform()
    {
        StopAllCoroutines();

        _rotated = !_rotated;
        Quaternion targetRotation = _rotated
            ? Quaternion.AngleAxis(rotationAngle, AxisToDirection(rotationAxis)) * _startRotation
            : _startRotation;

        Vector3 pivot = transform.TransformPoint(rotationPivotOffset);
        StartCoroutine(RotateTo(targetRotation, pivot));
    }

    IEnumerator MoveTo(Vector3 targetPosition)
    {
        float timeElapsed = 0;
        Vector3 startPosition = transform.position;
        while (timeElapsed < duration)
        {
            transform.position = Vector3.Lerp(startPosition, targetPosition, timeElapsed / duration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition;
    }

    IEnumerator RotateTo(Quaternion targetRotation, Vector3 pivot)
    {
        float timeElapsed = 0;
        Quaternion startRotation = transform.rotation;
        Vector3 pivotOffset = Quaternion.Inverse(startRotation) * (transform.position - pivot);
        while (timeElapsed < duration)
        {
            Quaternion rotation = Quaternion.Slerp(startRotation, targetRotation, timeElapsed / duration);
            transform.rotation = rotation;
            transform.position = pivot + rotation * pivotOffset;
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRotation;
        transform.position = pivot + targetRotation * pivotOffset;
    }

    private static Vector3 AxisToDirection(Axis axis)
    {
        switch (axis)
        {
            case Axis.X:
                return Vector3.right;
            case Axis.Z:
                return Vector3.forward;
            default:
                return Vector3.up;
        }
    }
}
