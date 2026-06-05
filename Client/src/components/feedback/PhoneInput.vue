<script setup lang="ts">
import { ref, watch } from 'vue';
import { VueTelInput } from 'vue-tel-input';
import 'vue-tel-input/vue-tel-input.css';

const props = defineProps<{ modelValue?: string; label?: string; invalid?: boolean; dense?: boolean }>();
const emit = defineEmits<{
  'update:modelValue': [value: string];
  validity: [valid: boolean];
}>();

const display = ref(props.modelValue ?? '');

interface PhoneObject {
  valid?: boolean;
  number?: string;
}

watch(
  () => props.modelValue,
  (next) => {
    if ((next ?? '') !== display.value) {
      display.value = next ?? '';
    }
  },
);

function onInput(_: string, phone: PhoneObject) {
  emit('update:modelValue', phone.valid && phone.number ? phone.number : '');
  emit('validity', Boolean(phone.valid));
}
</script>

<template>
  <div class="phone-field" :class="{ 'phone-field--invalid': invalid, 'phone-field--dense': dense }">
    <VueTelInput
      v-model="display"
      mode="international"
      default-country="UA"
      :auto-default-country="false"
      :ignored-countries="['RU', 'BY']"
      :input-options="{ placeholder: '' }"
      @on-input="onInput"
    />
    <label v-if="label" class="phone-label">{{ label }}</label>
  </div>
</template>

<style scoped>
.phone-field {
  position: relative;
}
.phone-field :deep(.vue-tel-input) {
  height: 2.75rem;
  border: 1px solid #94a3b8;
  border-radius: 0;
  box-shadow: none;
}
.phone-field--dense :deep(.vue-tel-input) {
  height: 2.5rem;
}
.phone-field :deep(.vue-tel-input:focus-within) {
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
}
.phone-field--invalid :deep(.vue-tel-input) {
  border-color: #ef4444;
}
.phone-field :deep(.vti__input) {
  font-size: 14px;
  background: transparent;
}
.phone-field :deep(.vti__dropdown) {
  border-radius: 0;
}
.phone-field :deep(.vti__dropdown:hover),
.phone-field :deep(.vti__dropdown.open) {
  background: #f1f5f9;
}
.phone-label {
  position: absolute;
  top: 0;
  left: 0.5rem;
  transform: translateY(-50%);
  padding: 0 0.25rem;
  background: #fff;
  font-size: 12px;
  color: #64748b;
  pointer-events: none;
}
</style>
