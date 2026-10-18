using DAARPG.persistence.domain;

namespace DAARPG.persistence.serializer;

public interface ISaveSerializer
{
    string Serialize(GameSave save);

    GameSave Deserialize(string data);
}
