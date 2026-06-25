public static class MqttTopicParser
{
    public static bool TryParseTwinTelemetryTopic(
        string topic,
        out string cellId,
        out string objectId,
        out string metric)
    {
        cellId = string.Empty;
        objectId = string.Empty;
        metric = string.Empty;

        if (string.IsNullOrWhiteSpace(topic))
        {
            return false;
        }

        string[] parts = topic.Split('/');
        if (parts.Length != 5)
        {
            return false;
        }

        if (parts[0] != "factory" || parts[2] != "twin")
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(parts[1]) ||
            string.IsNullOrWhiteSpace(parts[3]) ||
            string.IsNullOrWhiteSpace(parts[4]))
        {
            return false;
        }

        cellId = parts[1];
        objectId = parts[3];
        metric = parts[4];
        return true;
    }
}
