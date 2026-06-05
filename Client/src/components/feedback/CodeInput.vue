<script setup lang="ts">
import { ref, watch } from 'vue';

const props = withDefaults(
  defineProps<{ modelValue: string; length?: number; disabled?: boolean; numeric?: boolean }>(),
  { length: 6, disabled: false, numeric: false },
);
const emit = defineEmits<{
  'update:modelValue': [value: string];
  complete: [value: string];
}>();

const boxes = ref<HTMLInputElement[]>([]);

function digits(): string[] {
  return Array.from({ length: props.length }, (_, i) => props.modelValue[i] ?? '');
}

function emitValue(next: string) {
  emit('update:modelValue', next);
  if (next.length === props.length) emit('complete', next);
}

function sanitize(value: string): string {
  if (props.numeric) return value.replace(/\D/g, '');
  return value.replace(/[^a-zA-Z0-9]/g, '').toUpperCase();
}

function onInput(index: number, event: Event) {
  const target = event.target as HTMLInputElement;
  const char = sanitize(target.value).slice(-1);
  const current = digits();
  current[index] = char;
  target.value = char;
  emitValue(current.join('').slice(0, props.length));
  if (char && index < props.length - 1) {
    boxes.value[index + 1]?.focus();
  }
}

function onKeydown(index: number, event: KeyboardEvent) {
  if (event.key === 'Backspace' && !digits()[index] && index > 0) {
    boxes.value[index - 1]?.focus();
  }
}

function onPaste(event: ClipboardEvent) {
  event.preventDefault();
  const pasted = sanitize(event.clipboardData?.getData('text') ?? '').slice(0, props.length);
  if (!pasted) return;
  emitValue(pasted);
  const focusIndex = Math.min(pasted.length, props.length - 1);
  boxes.value[focusIndex]?.focus();
}

watch(
  () => props.modelValue,
  (next) => {
    if (!next) {
      boxes.value.forEach((b) => (b.value = ''));
    }
  },
);
</script>

<template>
  <div class="flex justify-center gap-2" @paste="onPaste">
    <input
      v-for="(d, i) in digits()"
      :key="i"
      :ref="(el) => { if (el) boxes[i] = el as HTMLInputElement; }"
      type="text"
      :inputmode="numeric ? 'numeric' : 'text'"
      autocapitalize="characters"
      autocomplete="one-time-code"
      maxlength="1"
      :value="d"
      :disabled="disabled"
      class="code-box"
      @input="onInput(i, $event)"
      @keydown="onKeydown(i, $event)"
    />
  </div>
</template>

<style scoped>
.code-box {
  width: 2.75rem;
  height: 3.25rem;
  text-align: center;
  font-size: 1.25rem;
  font-weight: 700;
  text-transform: uppercase;
  color: #0f172a;
  border: 1.5px solid #cbd5e1;
  background: #fff;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.code-box:focus {
  outline: none;
  border-color: #2563eb;
  box-shadow: 0 0 0 3px rgba(37, 99, 235, 0.12);
}
.code-box:disabled {
  background: #f1f5f9;
  color: #94a3b8;
}
</style>
