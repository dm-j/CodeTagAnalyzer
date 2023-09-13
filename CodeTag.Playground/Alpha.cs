namespace CodeTag.Playground;

[EnableCodeTag]
public class Robot
{
    public string Model { get; set; } = default!;
    public string SerialNumber { get; set; } = default!;
    private RobotController Controller { get; set; } = default!;

    [CodeTag("ROBOT-MOVE")]
    public void MoveForward(int distance)
    {
        Controller.Move(distance);
        Console.WriteLine($"{Model} moving forward by {distance} units.");
    }

    [CodeTag("ROBOT-MOVE")]
    [CodeTag("ROBOT-BRAKES")]
    public void Stop()
    {
        Controller.Move(0);
        Controller.ApplyBrakes(true);
        Console.WriteLine($"{Model} has stopped.");
    }

    [CodeTag("ROBOT-MOVE")]
    public void MoveBackward(int distance)
    {
        Controller.Move(-distance);
        Console.WriteLine($"{Model} moving backward by {distance} units.");
    }
}

public class RobotController
{
    [DefineCodeTag("ROBOT-MOVE")]
    public void Move(int distance)
    {
      // ... code omitted
    }

    [DefineCodeTag("ROBOT-BRAKES")]
    public void ApplyBrakes(bool onOff)
    {
        // ... code omitted
    }
}
