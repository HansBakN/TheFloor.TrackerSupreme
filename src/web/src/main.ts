import { createPinia } from 'pinia'
import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import Home from './Home.vue'
import Host from './Host.vue'
import Display from './Display.vue'
import Rehearsal from './Rehearsal.vue'
import RehearsalDisplay from './RehearsalDisplay.vue'
import CategoryEditor from './CategoryEditor.vue'
import SetEditor from './SetEditor.vue'
import './style.css'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Home },
    { path: '/host', component: Host },
    { path: '/display', component: Display },
    { path: '/display/rehearsal/:id', component: RehearsalDisplay },
    { path: '/rehearsal', component: Rehearsal },
    { path: '/sets', component: SetEditor },
    { path: '/categories/:id?', component: CategoryEditor },
  ],
})

createApp(App).use(createPinia()).use(router).mount('#app')
