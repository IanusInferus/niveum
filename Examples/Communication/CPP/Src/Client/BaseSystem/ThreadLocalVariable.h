#pragma once

#include <functional>
#include <memory>
#include <unordered_map>
#include <mutex>
#include <thread>

namespace BaseSystem
{
namespace detail
{
    std::function<void()> do_at_thread_exit(std::function<void()> f);
}

    template <typename T>
    class ThreadLocalVariable final
    {
    private:
        class ControlBlock
        {
        public:
            std::mutex lockee_;
            std::function<T()> factory_;
            std::unordered_map<std::thread::id, T> mappings_;
            std::unordered_map<std::thread::id, std::function<void()>> cancels_;
        };
        std::shared_ptr<ControlBlock> block_;
    public:
        ThreadLocalVariable(std::function<T()> factory)
        {
            if (factory == nullptr) {
                throw std::invalid_argument("Factory must be present.");
            }
            block_ = std::make_shared<ControlBlock>();
            block_->factory_ = factory;
        }

        ~ThreadLocalVariable()
        {
            std::unordered_map<std::thread::id, std::function<void()>> cancels;
            {
                std::lock_guard<std::mutex> lock(block_->lockee_);
                block_->mappings_.clear();
                block_->factory_ = nullptr;
                cancels = std::move(block_->cancels_);
            }
            block_ = nullptr;
            for (auto p : cancels) {
                std::get<1>(p)();
            }
        }

        T & Value()
        {
            auto id = std::this_thread::get_id();

            {
                std::lock_guard<std::mutex> lock(block_->lockee_);
                if (block_->mappings_.count(id) > 0) {
                    return block_->mappings_[id];
                } else {
                    auto v = block_->factory_();
                    block_->mappings_[id] = std::move(v);
                    auto block = block_;
                    block_->cancels_[id] = detail::do_at_thread_exit([block, id]()
                    {
                        std::lock_guard<std::mutex> lock(block->lockee_);
                        if (block->mappings_.count(id) > 0) {
                            block->mappings_.erase(id);
                        }
                        if (block->cancels_.count(id) > 0) {
                            block->cancels_.erase(id);
                        }
                    });
                    return block_->mappings_[id];
                }
            }
        }
    };
}
