#include "ServerContext.h"
#include "CommunicationBinary.h"
#include "BaseSystem/Strings.h"

using namespace Server;

ServerContext::ServerContext()
{
    Communication::Binary::BinarySerializationServer bss;
    HeadCommunicationSchemaHash = ToHexString(bss.Hash());
}
