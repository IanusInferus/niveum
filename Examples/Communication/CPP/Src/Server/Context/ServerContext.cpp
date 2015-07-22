#include "ServerContext.h"
#include "CommunicationBinary.h"
#include "BaseSystem/StringUtilities.h"

using namespace Server;

ServerContext::ServerContext()
{
    Communication::Binary::BinarySerializationServer bss;
    HeadCommunicationSchemaHash = ToHexString(bss.Hash());
}
