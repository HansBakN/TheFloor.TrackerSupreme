import { createPinia } from 'pinia'
import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import Home from './Home.vue'
import './style.css'

const router = createRouter({
  history: createWebHistory(),
  routes: [{ path: '/', component: Home }],
})

createApp(App).use(createPinia()).use(router).mount('#app')
