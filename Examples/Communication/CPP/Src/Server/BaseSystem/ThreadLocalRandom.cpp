#include "ThreadLocalRandom.h"

namespace BaseSystem
{

thread_local std::default_random_engine ThreadLocalRandom::re = std::default_random_engine(std::random_device()());

}
